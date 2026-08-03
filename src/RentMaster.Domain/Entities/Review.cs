using RentMaster.Domain.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Domain.Entities;

public sealed class Review : BaseEntity
{
    public Guid TenancyId { get; set; }
    public required string ReviewerUserId { get; set; }
    public required string SubjectUserId { get; set; }
    public ReviewDirection Direction { get; set; }
    public int OverallRating { get; set; }
    public string? Comment { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Published;
    public string? ModerationReason { get; set; }
    public string? ModeratedByUserId { get; set; }
    public DateTimeOffset? ModeratedAtUtc { get; set; }

    public Tenancy Tenancy { get; set; } = null!;
    public ICollection<ReviewScore> Scores { get; set; } = [];
    public ICollection<ReviewDispute> Disputes { get; set; } = [];
}
