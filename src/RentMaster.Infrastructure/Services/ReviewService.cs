using System.Text.RegularExpressions;
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

        if (tenancy.Status != TenancyStatus.Ended ||
            string.IsNullOrWhiteSpace(tenancy.EndRequestedByUserId) ||
            string.IsNullOrWhiteSpace(tenancy.EndApprovedByUserId) ||
            tenancy.ActualEndDate is null)
        {
            throw new ConflictException(
                "Reviews become available only after both parties approve the tenancy closure and the final handover is completed.");
        }

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

        if (request.CategoryScores is null)
            throw new ValidationException("Category scores are required.");

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

        ValidatePublicComment(request.Comment);

        var exists = await dbContext.Reviews.AnyAsync(
            x => x.TenancyId == tenancy.Id && x.ReviewerUserId == currentUser.UserId,
            cancellationToken);

        if (exists)
            throw new ConflictException("You have already reviewed this tenancy.");

        var publishedAt = DateTimeOffset.UtcNow;
        var review = new Review
        {
            TenancyId = tenancy.Id,
            ReviewerUserId = currentUser.UserId,
            SubjectUserId = subjectUserId,
            Direction = direction,
            OverallRating = request.OverallRating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Status = ReviewStatus.Published,
            ModeratedAtUtc = publishedAt,
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
            .Where(x => x.SubjectUserId == userId && x.Status == ReviewStatus.Published)
            .OrderByDescending(x => x.ModeratedAtUtc ?? x.CreatedAtUtc);

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
            x.ModeratedAtUtc ?? x.CreatedAtUtc,
            x.Disputes.Any(dispute => !dispute.IsResolved)))
            .ToArray();

        return new PagedResult<PublicReviewDto>(items, page, pageSize, total);
    }

    public async Task DisputeAsync(
        Guid reviewId,
        DisputeReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            throw new ValidationException("A reason of at most 1000 characters is required.");

        var review = await dbContext.Reviews
            .Include(x => x.Disputes)
            .SingleOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Review was not found.");

        if (review.SubjectUserId != currentUser.UserId)
            throw new ForbiddenException("Only the reviewed user can report this review.");

        if (review.Status != ReviewStatus.Published)
            throw new ConflictException("Only a published review can be reported.");

        if (review.Disputes.Any(x => !x.IsResolved))
            throw new ConflictException("This review has already been reported and is waiting for a decision.");

        // Reporting creates a review request for an authorised reviewer. The published
        // feedback remains visible until a decision is recorded, so a report alone
        // cannot suppress a legitimate review.
        review.Disputes.Add(new ReviewDispute
        {
            RaisedByUserId = currentUser.UserId,
            Reason = request.Reason.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<DisputedReviewDto>> GetDisputedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureReviewer();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Reviews.AsNoTracking()
            .Include(x => x.Scores)
            .Include(x => x.Disputes)
            .Where(x => x.Status != ReviewStatus.Rejected &&
                        x.Disputes.Any(dispute => !dispute.IsResolved))
            .OrderBy(x => x.UpdatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var reviews = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = reviews.Select(review =>
        {
            var dispute = review.Disputes
                .Where(item => !item.IsResolved)
                .OrderBy(item => item.CreatedAtUtc)
                .First();

            return new DisputedReviewDto(
                review.Id,
                dispute.Id,
                review.TenancyId,
                review.ReviewerUserId,
                review.SubjectUserId,
                review.Direction,
                review.OverallRating,
                review.Scores.Select(score => new ReviewScoreDto(score.Category, score.Score)).ToArray(),
                review.Comment,
                dispute.Reason,
                dispute.CreatedAtUtc);
        }).ToArray();

        return new PagedResult<DisputedReviewDto>(items, page, pageSize, total);
    }

    public async Task ResolveDisputeAsync(
        Guid reviewId,
        ResolveReviewDisputeRequest request,
        CancellationToken cancellationToken)
    {
        EnsureReviewer();
        var resolution = request.Resolution?.Trim();
        if (string.IsNullOrWhiteSpace(resolution) || resolution.Length > 1000)
            throw new ValidationException("A decision note of at most 1000 characters is required.");

        var review = await dbContext.Reviews
            .Include(x => x.Disputes)
            .SingleOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Review was not found.");

        var dispute = review.Disputes
            .Where(x => !x.IsResolved)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefault()
            ?? throw new ConflictException("The review has no unresolved report.");

        var now = DateTimeOffset.UtcNow;
        dispute.IsResolved = true;
        dispute.Resolution = resolution;
        dispute.ResolvedByUserId = currentUser.UserId;
        dispute.ResolvedAtUtc = now;

        review.Status = request.Republish ? ReviewStatus.Published : ReviewStatus.Rejected;
        review.ModerationReason = request.Republish ? null : resolution[..Math.Min(500, resolution.Length)];
        review.ModeratedByUserId = currentUser.UserId;
        review.ModeratedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePublicComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return;

        var value = comment.Trim();
        var containsEmail = Regex.IsMatch(
            value,
            @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        var containsIndianPhone = Regex.IsMatch(
            value,
            @"(?<!\d)(?:\+?91[ -]?)?[6-9]\d{9}(?!\d)",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        var containsLongIdentityNumber = Regex.IsMatch(
            value,
            @"(?<!\d)(?:\d[ -]?){12}(?!\d)",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        if (containsEmail || containsIndianPhone || containsLongIdentityNumber)
        {
            throw new ValidationException(
                "Remove phone numbers, email addresses and identity-document numbers from the public review.");
        }
    }

    private void EnsureReviewer()
    {
        if (!currentUser.IsInRole(AppRoles.Admin) && !currentUser.IsInRole(AppRoles.Moderator))
            throw new ForbiddenException("You do not have permission to review reported feedback.");
    }
}
