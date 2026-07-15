using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class ReviewDispute : BaseEntity
{
    public Guid ReviewId { get; set; }
    public required string RaisedByUserId { get; set; }
    public required string Reason { get; set; }
    public bool IsResolved { get; set; }
    public string? Resolution { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public Review Review { get; set; } = null!;
}
