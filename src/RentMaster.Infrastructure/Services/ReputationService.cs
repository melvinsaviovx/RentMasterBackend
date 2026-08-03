using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class ReputationService(
    AppDbContext dbContext,
    ICurrentUserService currentUser)
    : IReputationService
{
    public async Task<ReputationProfileDto> GetByCodeAsync(
        string profileCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profileCode))
            throw new ValidationException("Profile code is required.");

        var acceptedCodes = BuildAcceptedCodes(profileCode);
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(
                account => account.IsActive && acceptedCodes.Contains(account.PublicProfileCode),
                cancellationToken)
            ?? throw new NotFoundException("No reputation profile was found for this code.");

        return await BuildProfileAsync(user, page, pageSize, cancellationToken);
    }

    public async Task<ReputationProfileDto> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Please sign in to view your reputation profile.");

        var userId = currentUser.UserId;
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(account => account.Id == userId && account.IsActive, cancellationToken)
            ?? throw new NotFoundException("Your reputation profile could not be found.");

        return await BuildProfileAsync(user, page, pageSize, cancellationToken);
    }

    private async Task<ReputationProfileDto> BuildProfileAsync(
        ApplicationUser user,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Load the small review set in separate, predictable queries. This avoids a
        // multiple-collection join and keeps profiles with zero reviews reliable.
        var reviews = await dbContext.Reviews.AsNoTracking()
            .Where(review =>
                review.SubjectUserId == user.Id &&
                review.Status == ReviewStatus.Published)
            .OrderByDescending(review => review.ModeratedAtUtc ?? review.CreatedAtUtc)
            .Select(review => new ReviewRow(
                review.Id,
                review.Direction,
                review.OverallRating,
                review.Comment,
                review.ModeratedAtUtc ?? review.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var reviewIds = reviews.Select(review => review.Id).ToArray();
        var scores = reviewIds.Length == 0
            ? new List<ScoreRow>()
            : await dbContext.ReviewScores.AsNoTracking()
                .Where(score => reviewIds.Contains(score.ReviewId))
                .Select(score => new ScoreRow(score.ReviewId, score.Category, score.Score))
                .ToListAsync(cancellationToken);

        var disputedReviewIds = reviewIds.Length == 0
            ? new HashSet<Guid>()
            : (await dbContext.ReviewDisputes.AsNoTracking()
                .Where(dispute => reviewIds.Contains(dispute.ReviewId) && !dispute.IsResolved)
                .Select(dispute => dispute.ReviewId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var scoresByReview = scores
            .GroupBy(score => score.ReviewId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(score => score.Category, StringComparer.Ordinal)
                    .ToArray());

        var total = reviews.Count;
        var average = total == 0
            ? (double?)null
            : Math.Round(reviews.Average(review => review.OverallRating), 2);

        var ratingDistribution = Enumerable.Range(1, 5)
            .OrderByDescending(rating => rating)
            .Select(rating => new RatingDistributionDto(
                rating,
                reviews.Count(review => review.OverallRating == rating)))
            .ToArray();

        var categoryAverages = scores
            .GroupBy(score => score.Category, StringComparer.Ordinal)
            .Select(group => new CategoryRatingAverageDto(
                group.Key,
                Math.Round(group.Average(score => score.Score), 2)))
            .OrderByDescending(item => item.Average)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ToArray();

        var pageItems = reviews
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(review => new PublicReviewDto(
                review.Id,
                review.Direction,
                review.Direction == ReviewDirection.OwnerToTenant
                    ? "Verified owner"
                    : "Verified tenant",
                review.OverallRating,
                scoresByReview.GetValueOrDefault(review.Id, Array.Empty<ScoreRow>())
                    .Select(score => new ReviewScoreDto(score.Category, score.Score))
                    .ToArray(),
                review.Comment,
                review.PublishedAtUtc,
                disputedReviewIds.Contains(review.Id)))
            .ToArray();

        var roleNames = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id && role.Name != null
            orderby role.Name
            select role.Name!)
            .ToListAsync(cancellationToken);

        var isIdentityVerified = await dbContext.IdentityDocuments.AsNoTracking().AnyAsync(
            document => document.UserId == user.Id &&
                        document.Status == VerificationStatus.Verified,
            cancellationToken);

        return new ReputationProfileDto(
            user.PublicProfileCode,
            CreateDisplayName(user.FullName),
            roleNames,
            isIdentityVerified,
            average,
            total,
            ratingDistribution,
            categoryAverages,
            new PagedResult<PublicReviewDto>(pageItems, page, pageSize, total));
    }

    private static string[] BuildAcceptedCodes(string profileCode)
    {
        var normalizedCode = profileCode.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        var codeWithoutPrefix = normalizedCode.StartsWith("RM-", StringComparison.Ordinal)
            ? normalizedCode[3..]
            : normalizedCode;

        return new[]
        {
            normalizedCode,
            codeWithoutPrefix,
            $"RM-{codeWithoutPrefix}"
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string CreateDisplayName(string fullName)
    {
        var parts = fullName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return "Verified user";

        if (parts.Length == 1)
            return parts[0];

        return $"{parts[0]} {parts[^1][0]}.";
    }

    private sealed record ReviewRow(
        Guid Id,
        ReviewDirection Direction,
        int OverallRating,
        string? Comment,
        DateTimeOffset PublishedAtUtc);

    private sealed record ScoreRow(Guid ReviewId, string Category, int Score);
}
