using RentMaster.Domain.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Domain.Entities;

public sealed class MaintenanceRequest : BaseEntity
{
    public Guid TenancyId { get; set; }
    public Guid PropertyId { get; set; }
    public required string CreatedByUserId { get; set; }
    public string? AssignedToUserId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Normal;
    public MaintenanceRequestStatus Status { get; set; } = MaintenanceRequestStatus.New;
    public string? MaintenanceNote { get; set; }
    public DateTimeOffset? AssignedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }

    public Tenancy Tenancy { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
