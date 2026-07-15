using RentMaster.Domain.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Domain.Entities;

public sealed class RentalApplication : BaseEntity
{
    public Guid PropertyId { get; set; }
    public required string TenantUserId { get; set; }
    public DateOnly ExpectedMoveInDate { get; set; }
    public DateOnly? ExpectedMoveOutDate { get; set; }
    public int OccupantCount { get; set; }
    public required string Message { get; set; }
    public RentalApplicationStatus Status { get; set; } = RentalApplicationStatus.Submitted;
    public DateTimeOffset? DecisionAtUtc { get; set; }
    public string? DecisionByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public Guid? TenancyId { get; set; }

    public Property Property { get; set; } = null!;
    public Tenancy? Tenancy { get; set; }
}
