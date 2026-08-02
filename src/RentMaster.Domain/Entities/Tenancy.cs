using RentMaster.Domain.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Domain.Entities;

public sealed class Tenancy : BaseEntity
{
    public Guid PropertyId { get; set; }
    public required string OwnerUserId { get; set; }
    public required string TenantUserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? ExpectedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public decimal AgreedMonthlyRent { get; set; }
    public decimal AgreedSecurityDeposit { get; set; }
    public TenancyStatus Status { get; set; } = TenancyStatus.PendingTenantConfirmation;
    public string? EndRequestedByUserId { get; set; }
    public DateTimeOffset? EndRequestedAtUtc { get; set; }
    public DateOnly? RequestedEndDate { get; set; }
    public string? EndRequestReason { get; set; }
    public string? EndApprovedByUserId { get; set; }
    public DateTimeOffset? EndApprovedAtUtc { get; set; }

    public Property Property { get; set; } = null!;
    public ICollection<Review> Reviews { get; set; } = [];
}
