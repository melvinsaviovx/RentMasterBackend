using RentMaster.Application.Common;

namespace RentMaster.Application.Contracts;

public sealed record ReputationProfileDto(
    string PublicProfileCode,
    string DisplayName,
    IReadOnlyList<string> Roles,
    double? AverageRating,
    int TotalPublishedReviews,
    PagedResult<PublicReviewDto> Reviews);
