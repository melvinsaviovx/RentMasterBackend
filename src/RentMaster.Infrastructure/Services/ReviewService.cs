using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class ReviewService(
    AppDbContext dbContext,
    ICurrentUserService currentUser)
    : IReviewService
{
    private static readonly string[] OwnerToTenantCategories =
        ["RentPayment", "PropertyCare", "NeighbourConduct", "Communication"];

    private static readonly string[] TenantToOwnerCategories =
        ["MaintenanceResponse", "Communication", "PrivacyRespect", "DepositFairness"];

    public async Task<Guid> SubmitAsync(
        SubmitReviewRequest request,
        CancellationToken cancellationToken)
    {
        var tenancy = await dbContext.Tenancies
            .SingleOrDefaultAsync(x => x.Id == request.TenancyId, cancellationToken)
            ?? throw new NotFoundException("Tenancy was not found.");

        if (tenancy.Status != TenancyStatus.Ended)
            throw new ConflictException("Reviews are allowed only after the tenancy has ended.");

        ReviewDirection direction;
        string subjectUserId;
        string[] requiredCategories;

        if (tenancy.OwnerUserId == currentUser.UserId)
        {
            direction = ReviewDirection.OwnerToTenant;
            subjectUserId = tenancy.TenantUserId;
            requiredCategories = OwnerToTenantCategories;
        }
        else if (tenancy.TenantUserId == currentUser.UserId)
        {
            direction = ReviewDirection.TenantToOwner;
            subjectUserId = tenancy.OwnerUserId;
            requiredCategories = TenantToOwnerCategories;
        }
        else
        {
            throw new ForbiddenException("You are not a party to this tenancy.");
        }

        if (request.OverallRating is < 1 or > 5)
            throw new ValidationException("Overall rating must be between 1 and 5.");

        if (!requiredCategories.All(request.CategoryScores.ContainsKey) ||
            request.CategoryScores.Keys.Any(x => !requiredCategories.Contains(x, StringComparer.Ordinal)))
        {
            throw new ValidationException(
                $"Required categories: {string.Join(", ", requiredCategories)}.");
        }

        if (request.CategoryScores.Values.Any(x => x is < 1 or > 5))
            throw new ValidationException("Every category score must be between 1 and 5.");

        if (request.Comment?.Length > 1500)
            throw new ValidationException("Review comment must be at most 1500 characters.");

        var exists = await dbContext.Reviews.AnyAsync(
            x => x.TenancyId == tenancy.Id && x.ReviewerUserId == currentUser.UserId,
            cancellationToken);

        if (exists)
            throw new ConflictException("You have already reviewed this tenancy.");

        var review = new Review
        {
            TenancyId = tenancy.Id,
            ReviewerUserId = currentUser.UserId,
            SubjectUserId = subjectUserId,
            Direction = direction,
            OverallRating = request.OverallRating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Scores = request.CategoryScores.Select(x => new ReviewScore
            {
                Category = x.Key,
                Score = x.Value
            }).ToList()
        };

        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        return review.Id;
    }

    public async Task<PagedResult<PublicReviewDto>> GetPublishedForUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Reviews.AsNoTracking()
            .Include(x => x.Scores)
            .Include(x => x.Disputes)
            .Where(x => x.SubjectUserId == userId &&
                        (x.Status == ReviewStatus.Published || x.Status == ReviewStatus.Disputed))
            .OrderByDescending(x => x.ModeratedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var reviews = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = reviews.Select(x => new PublicReviewDto(
            x.Id,
            x.Direction,
            x.Direction == ReviewDirection.OwnerToTenant ? "Verified owner" : "Verified tenant",
            x.OverallRating,
            x.Scores.Select(s => new ReviewScoreDto(s.Category, s.Score)).ToArray(),
            x.Comment,
            x.ModeratedAtUtc ?? x.UpdatedAtUtc,
            x.Status == ReviewStatus.Disputed ||
                x.Disputes.Any(d => !d.IsResolved)))
            .ToArray();

        return new PagedResult<PublicReviewDto>(items, page, pageSize, total);
    }

    public async Task DisputeAsync(
        Guid reviewId,
        DisputeReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            throw new ValidationException("A dispute reason of at most 1000 characters is required.");

        var review = await dbContext.Reviews
            .Include(x => x.Disputes)
            .SingleOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Review was not found.");

        if (review.SubjectUserId != currentUser.UserId)
            throw new ForbiddenException("Only the reviewed user can dispute this review.");

        if (review.Status != ReviewStatus.Published)
            throw new ConflictException("Only a published review can be disputed.");

        if (review.Disputes.Any(x => !x.IsResolved))
            throw new ConflictException("An unresolved dispute already exists.");

        review.Status = ReviewStatus.Disputed;
        review.Disputes.Add(new ReviewDispute
        {
            RaisedByUserId = currentUser.UserId,
            Reason = request.Reason.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<PendingReviewDto>> GetPendingAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Reviews.AsNoTracking()
            .Include(x => x.Scores)
            .Where(x => x.Status == ReviewStatus.PendingModeration)
            .OrderBy(x => x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var reviews = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = reviews.Select(x => new PendingReviewDto(
            x.Id,
            x.TenancyId,
            x.ReviewerUserId,
            x.SubjectUserId,
            x.Direction,
            x.OverallRating,
            x.Scores.Select(s => new ReviewScoreDto(s.Category, s.Score)).ToArray(),
            x.Comment,
            x.CreatedAtUtc)).ToArray();

        return new PagedResult<PendingReviewDto>(items, page, pageSize, total);
    }

    public async Task DecideAsync(
        Guid reviewId,
        ReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.Reviews
            .SingleOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Review was not found.");

        if (review.Status != ReviewStatus.PendingModeration)
            throw new ConflictException("Only pending reviews can be moderated.");

        if (!request.Publish && string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("A rejection reason is required.");

        review.Status = request.Publish ? ReviewStatus.Published : ReviewStatus.Rejected;
        review.ModerationReason = request.Publish ? null : request.Reason!.Trim();
        review.ModeratedByUserId = currentUser.UserId;
        review.ModeratedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
