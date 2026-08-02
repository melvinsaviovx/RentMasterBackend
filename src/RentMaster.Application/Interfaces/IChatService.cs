using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IChatService
{
    Task<ChatConversationDto> OpenForPropertyAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<PagedResult<ChatConversationDto>> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<ChatMessageDto>> GetMessagesAsync(Guid conversationId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ChatMessageDto> SendAsync(Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<Guid> EnsureConversationAsync(Guid propertyId, string ownerUserId, string tenantUserId, Guid? applicationId, CancellationToken cancellationToken);
    Task AttachTenancyAsync(Guid propertyId, string ownerUserId, string tenantUserId, Guid tenancyId, CancellationToken cancellationToken);
    Task AddSystemMessageAsync(Guid propertyId, string ownerUserId, string tenantUserId, string content, CancellationToken cancellationToken);
}
