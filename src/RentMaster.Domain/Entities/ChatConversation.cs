using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class ChatConversation : BaseEntity
{
    public Guid PropertyId { get; set; }
    public required string OwnerUserId { get; set; }
    public required string TenantUserId { get; set; }
    public Guid? RentalApplicationId { get; set; }
    public Guid? TenancyId { get; set; }
    public DateTimeOffset? LastMessageAtUtc { get; set; }

    public Property Property { get; set; } = null!;
    public RentalApplication? RentalApplication { get; set; }
    public Tenancy? Tenancy { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
