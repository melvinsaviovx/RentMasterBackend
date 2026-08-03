using RentMaster.Domain.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Domain.Entities;

public sealed class SupportTicket : BaseEntity
{
    public required string UserId { get; set; }
    public required string Category { get; set; }
    public required string Subject { get; set; }
    public required string Description { get; set; }
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    public string? AdminReply { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}
