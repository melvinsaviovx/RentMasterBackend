using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost("properties/{propertyId:guid}/open")]
    public Task<ChatConversationDto> OpenForProperty(
        Guid propertyId,
        CancellationToken cancellationToken) =>
        chatService.OpenForPropertyAsync(propertyId, cancellationToken);

    [HttpGet("conversations")]
    public Task<PagedResult<ChatConversationDto>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        chatService.GetMineAsync(page, pageSize, cancellationToken);

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public Task<PagedResult<ChatMessageDto>> GetMessages(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        chatService.GetMessagesAsync(conversationId, page, pageSize, cancellationToken);

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public Task<ChatMessageDto> Send(
        Guid conversationId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken) =>
        chatService.SendAsync(conversationId, request, cancellationToken);

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        await chatService.MarkReadAsync(conversationId, cancellationToken);
        return NoContent();
    }
}
