using RentMaster.Application.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record CreateSupportTicketRequest(
    string Category,
    string Subject,
    string Description);

public sealed record SupportTicketDecisionRequest(
    SupportTicketStatus Status,
    string Reply);

public sealed record SupportTicketDto(
    Guid Id,
    string UserId,
    string UserFullName,
    string UserEmail,
    string Category,
    string Subject,
    string Description,
    SupportTicketStatus Status,
    string? AdminReply,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);
