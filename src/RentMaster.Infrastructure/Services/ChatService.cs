using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class ChatService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IChatService
{
    public async Task<ChatConversationDto> OpenForPropertyAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        await TouchPresenceAsync(cancellationToken);
        if (!currentUser.IsInRole(AppRoles.Tenant))
            throw new ForbiddenException("Only tenants can start a property conversation.");

        var property = await dbContext.Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken)
            ?? throw new NotFoundException("Property was not found.");

        if (property.OwnerUserId == currentUser.UserId)
            throw new ForbiddenException("You cannot start a conversation with yourself.");

        var existing = await dbContext.ChatConversations
            .SingleOrDefaultAsync(
                x => x.PropertyId == propertyId &&
                     x.OwnerUserId == property.OwnerUserId &&
                     x.TenantUserId == currentUser.UserId,
                cancellationToken);

        if (existing is null && property.Status != PropertyStatus.Published)
            throw new ConflictException("A conversation can be started only for an available property.");

        var conversationId = existing?.Id ?? await EnsureConversationAsync(
            propertyId,
            property.OwnerUserId,
            currentUser.UserId,
            applicationId: null,
            cancellationToken);

        return await MapConversationAsync(conversationId, cancellationToken);
    }

    public async Task<PagedResult<ChatConversationDto>> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await TouchPresenceAsync(cancellationToken);
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = dbContext.ChatConversations.AsNoTracking()
            .Where(x => x.OwnerUserId == currentUser.UserId || x.TenantUserId == currentUser.UserId)
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var ids = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // Loading the inbox means the recipient's chat client has received messages for
        // these conversations. Opening a conversation is still required before ReadAtUtc is set.
        await MarkDeliveredAsync(ids, cancellationToken);

        var items = new List<ChatConversationDto>(ids.Count);
        foreach (var id in ids)
            items.Add(await MapConversationAsync(id, cancellationToken));

        return new PagedResult<ChatConversationDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<ChatMessageDto>> GetMessagesAsync(
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await TouchPresenceAsync(cancellationToken);
        await EnsureParticipantAsync(conversationId, cancellationToken);
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = dbContext.ChatMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var messages = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var deliveryChanged = false;
        foreach (var message in messages)
        {
            if (!message.IsSystemMessage &&
                message.SenderUserId != currentUser.UserId &&
                message.DeliveredAtUtc is null)
            {
                message.DeliveredAtUtc = now;
                deliveryChanged = true;
            }
        }

        if (deliveryChanged)
            await dbContext.SaveChangesAsync(cancellationToken);

        var senderIds = messages.Select(x => x.SenderUserId).Distinct().ToArray();
        var senders = await dbContext.Users.AsNoTracking()
            .Where(x => senderIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        var items = messages
            .OrderBy(x => x.CreatedAtUtc)
            .Select(message => new ChatMessageDto(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.IsSystemMessage ? "Rent Master" : senders.GetValueOrDefault(message.SenderUserId, "User"),
                message.Content,
                message.IsSystemMessage,
                message.SenderUserId == currentUser.UserId && !message.IsSystemMessage,
                message.DeliveredAtUtc,
                message.ReadAtUtc,
                message.CreatedAtUtc))
            .ToArray();

        return new PagedResult<ChatMessageDto>(items, page, pageSize, total);
    }

    public async Task<ChatMessageDto> SendAsync(
        Guid conversationId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        await TouchPresenceAsync(cancellationToken);
        var conversation = await EnsureParticipantAsync(conversationId, cancellationToken);
        var content = NormalizeContent(request.Content);

        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderUserId = currentUser.UserId,
            Content = content
        };

        conversation.LastMessageAtUtc = DateTimeOffset.UtcNow;
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var senderName = await dbContext.Users.AsNoTracking()
            .Where(x => x.Id == currentUser.UserId)
            .Select(x => x.FullName)
            .SingleAsync(cancellationToken);

        return new ChatMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            senderName,
            message.Content,
            false,
            true,
            message.DeliveredAtUtc,
            message.ReadAtUtc,
            message.CreatedAtUtc);
    }

    public async Task MarkReadAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        await TouchPresenceAsync(cancellationToken);
        await EnsureParticipantAsync(conversationId, cancellationToken);
        var unread = await dbContext.ChatMessages
            .Where(x => x.ConversationId == conversationId &&
                        x.SenderUserId != currentUser.UserId &&
                        x.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var message in unread)
        {
            message.DeliveredAtUtc ??= now;
            message.ReadAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> EnsureConversationAsync(
        Guid propertyId,
        string ownerUserId,
        string tenantUserId,
        Guid? applicationId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.ChatConversations
            .SingleOrDefaultAsync(
                x => x.PropertyId == propertyId &&
                     x.OwnerUserId == ownerUserId &&
                     x.TenantUserId == tenantUserId,
                cancellationToken);

        if (conversation is not null)
        {
            if (applicationId.HasValue && conversation.RentalApplicationId != applicationId)
            {
                // A property/tenant pair keeps one continuous thread. When the same
                // tenant applies again after an older tenancy has ended, point the
                // thread at the newest application and clear the old tenancy context.
                conversation.RentalApplicationId = applicationId;
                conversation.TenancyId = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return conversation.Id;
        }

        conversation = new ChatConversation
        {
            PropertyId = propertyId,
            OwnerUserId = ownerUserId,
            TenantUserId = tenantUserId,
            RentalApplicationId = applicationId
        };

        dbContext.ChatConversations.Add(conversation);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return conversation.Id;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(conversation).State = EntityState.Detached;
            return await dbContext.ChatConversations
                .Where(x => x.PropertyId == propertyId &&
                            x.OwnerUserId == ownerUserId &&
                            x.TenantUserId == tenantUserId)
                .Select(x => x.Id)
                .SingleAsync(cancellationToken);
        }
    }

    public async Task AttachTenancyAsync(
        Guid propertyId,
        string ownerUserId,
        string tenantUserId,
        Guid tenancyId,
        CancellationToken cancellationToken)
    {
        var conversationId = await EnsureConversationAsync(
            propertyId,
            ownerUserId,
            tenantUserId,
            applicationId: null,
            cancellationToken);

        var conversation = await dbContext.ChatConversations
            .SingleAsync(x => x.Id == conversationId, cancellationToken);
        conversation.TenancyId = tenancyId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSystemMessageAsync(
        Guid propertyId,
        string ownerUserId,
        string tenantUserId,
        string content,
        CancellationToken cancellationToken)
    {
        var conversationId = await EnsureConversationAsync(
            propertyId,
            ownerUserId,
            tenantUserId,
            applicationId: null,
            cancellationToken);

        var conversation = await dbContext.ChatConversations
            .SingleAsync(x => x.Id == conversationId, cancellationToken);
        var initiatingUserId = currentUser.UserId == ownerUserId || currentUser.UserId == tenantUserId
            ? currentUser.UserId
            : ownerUserId;

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderUserId = initiatingUserId,
            Content = NormalizeContent(content),
            IsSystemMessage = true
        };

        conversation.LastMessageAtUtc = DateTimeOffset.UtcNow;
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ChatConversation> EnsureParticipantAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.ChatConversations
            .SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        if (conversation.OwnerUserId != currentUser.UserId &&
            conversation.TenantUserId != currentUser.UserId)
        {
            throw new ForbiddenException("You are not a participant in this conversation.");
        }

        return conversation;
    }

    private async Task<ChatConversationDto> MapConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var result = await (
            from conversation in dbContext.ChatConversations.AsNoTracking()
            join property in dbContext.Properties.AsNoTracking()
                on conversation.PropertyId equals property.Id
            join owner in dbContext.Users.AsNoTracking()
                on conversation.OwnerUserId equals owner.Id
            join tenant in dbContext.Users.AsNoTracking()
                on conversation.TenantUserId equals tenant.Id
            where conversation.Id == conversationId &&
                  (conversation.OwnerUserId == currentUser.UserId ||
                   conversation.TenantUserId == currentUser.UserId)
            select new { Conversation = conversation, Property = property, Owner = owner, Tenant = tenant })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var lastMessage = await dbContext.ChatMessages.AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.Content)
            .FirstOrDefaultAsync(cancellationToken);

        var unreadCount = await dbContext.ChatMessages.AsNoTracking()
            .CountAsync(x => x.ConversationId == conversationId &&
                             x.SenderUserId != currentUser.UserId &&
                             x.ReadAtUtc == null,
                cancellationToken);

        var counterparty = result.Conversation.OwnerUserId == currentUser.UserId
            ? result.Tenant
            : result.Owner;

        return new ChatConversationDto(
            result.Conversation.Id,
            result.Conversation.PropertyId,
            result.Property.Title,
            counterparty.FullName,
            counterparty.PublicProfileCode,
            counterparty.LastSeenAtUtc,
            result.Conversation.RentalApplicationId,
            result.Conversation.TenancyId,
            lastMessage is null ? null : Truncate(lastMessage, 90),
            result.Conversation.LastMessageAtUtc,
            unreadCount);
    }

    private async Task MarkDeliveredAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0)
            return;

        var undelivered = await dbContext.ChatMessages
            .Where(x => conversationIds.Contains(x.ConversationId) &&
                        !x.IsSystemMessage &&
                        x.SenderUserId != currentUser.UserId &&
                        x.DeliveredAtUtc == null)
            .ToListAsync(cancellationToken);

        if (undelivered.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var message in undelivered)
            message.DeliveredAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task TouchPresenceAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken);
        if (user is null)
            return;

        if (user.LastSeenAtUtc is null || now - user.LastSeenAtUtc.Value >= TimeSpan.FromSeconds(45))
        {
            user.LastSeenAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string NormalizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Message cannot be empty.");

        var value = content.Trim();
        if (value.Length > 2000)
            throw new ValidationException("Message must be at most 2000 characters.");

        return value;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : $"{value[..length]}…";

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
