using RentMaster.Application.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record SubmitReviewRequest(
    Guid TenancyId,
    int OverallRating,
    IReadOnlyDictionary<string, int> CategoryScores,
    string? Comment);

public sealed record ReviewScoreDto(string Category, int Score);

public sealed record PublicReviewDto(
    Guid Id,
    ReviewDirection Direction,
    string ReviewerType,
    int OverallRating,
    IReadOnlyList<ReviewScoreDto> Scores,
    string? Comment,
    DateTimeOffset PublishedAtUtc,
    bool IsDisputed);

public sealed record ReviewDecisionRequest(
    bool Publish,
    string? Reason);

public sealed record DisputeReviewRequest(string Reason);

public sealed record PendingReviewDto(
    Guid Id,
    Guid TenancyId,
    string ReviewerUserId,
    string SubjectUserId,
    ReviewDirection Direction,
    int OverallRating,
    IReadOnlyList<ReviewScoreDto> Scores,
    string? Comment,
    DateTimeOffset SubmittedAtUtc);

public sealed record DisputedReviewDto(
    Guid ReviewId,
    Guid DisputeId,
    Guid TenancyId,
    string ReviewerUserId,
    string SubjectUserId,
    ReviewDirection Direction,
    int OverallRating,
    IReadOnlyList<ReviewScoreDto> Scores,
    string? Comment,
    string DisputeReason,
    DateTimeOffset RaisedAtUtc);

public sealed record ResolveReviewDisputeRequest(
    bool Republish,
    string Resolution);
