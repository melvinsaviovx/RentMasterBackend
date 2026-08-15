using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class ChatMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public required string SenderUserId { get; set; }
    public required string Content { get; set; }
    public bool IsSystemMessage { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }

    public ChatConversation Conversation { get; set; } = null!;
}
