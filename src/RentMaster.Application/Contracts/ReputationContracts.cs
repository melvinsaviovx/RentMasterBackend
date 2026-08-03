using RentMaster.Application.Common;

namespace RentMaster.Application.Contracts;

public sealed record RatingDistributionDto(int Rating, int Count);

public sealed record CategoryRatingAverageDto(string Category, double Average);

public sealed record ReputationProfileDto(
    string PublicProfileCode,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool IsIdentityVerified,
    double? AverageRating,
    int TotalPublishedReviews,
    IReadOnlyList<RatingDistributionDto> RatingDistribution,
    IReadOnlyList<CategoryRatingAverageDto> CategoryAverages,
    PagedResult<PublicReviewDto> Reviews);
