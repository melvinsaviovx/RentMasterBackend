namespace RentMaster.Application.Contracts;

public sealed record ChatConversationDto(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    string CounterpartyDisplayName,
    string CounterpartyProfileCode,
    DateTimeOffset? CounterpartyLastSeenAtUtc,
    Guid? RentalApplicationId,
    Guid? TenancyId,
    string? LastMessagePreview,
    DateTimeOffset? LastMessageAtUtc,
    int UnreadCount);

public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    string SenderUserId,
    string SenderDisplayName,
    string Content,
    bool IsSystemMessage,
    bool IsMine,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? ReadAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record SendChatMessageRequest(string Content);
