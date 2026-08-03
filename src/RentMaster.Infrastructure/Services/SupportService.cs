using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class SupportService(
    AppDbContext dbContext,
    ICurrentUserService currentUser)
    : ISupportService
{
    public async Task<SupportTicketDto> CreateAsync(
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var ticket = new SupportTicket
        {
            UserId = currentUser.UserId,
            Category = request.Category.Trim(),
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim()
        };
        dbContext.SupportTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(ticket, cancellationToken);
    }

    public async Task<PagedResult<SupportTicketDto>> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var query = dbContext.SupportTickets.AsNoTracking()
            .Where(x => x.UserId == currentUser.UserId)
            .OrderByDescending(x => x.CreatedAtUtc);
        return await ToPageAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<SupportTicketDto>> GetAllAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureReviewer();
        (page, pageSize) = NormalizePage(page, pageSize);
        var query = dbContext.SupportTickets.AsNoTracking()
            .OrderBy(x => x.Status == SupportTicketStatus.Open ? 0 : x.Status == SupportTicketStatus.InProgress ? 1 : 2)
            .ThenByDescending(x => x.CreatedAtUtc);
        return await ToPageAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<SupportTicketDto> DecideAsync(
        Guid id,
        SupportTicketDecisionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureReviewer();
        if (!Enum.IsDefined(request.Status))
            throw new ValidationException("Support request status is invalid.");
        if (request.Status is SupportTicketStatus.Open)
            throw new ValidationException("Use In progress, Resolved or Closed for an admin decision.");
        if (string.IsNullOrWhiteSpace(request.Reply) || request.Reply.Trim().Length > 2000)
            throw new ValidationException("A reply of at most 2000 characters is required.");

        var ticket = await dbContext.SupportTickets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Support request was not found.");
        ticket.Status = request.Status;
        ticket.AdminReply = request.Reply.Trim();
        ticket.ResolvedByUserId = currentUser.UserId;
        ticket.ResolvedAtUtc = request.Status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed
            ? DateTimeOffset.UtcNow
            : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(ticket, cancellationToken);
    }

    private async Task<PagedResult<SupportTicketDto>> ToPageAsync(
        IQueryable<SupportTicket> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var tickets = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var userIds = tickets.Select(ticket => ticket.UserId).Distinct().ToArray();
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var items = tickets
            .Where(ticket => users.ContainsKey(ticket.UserId))
            .Select(ticket => Map(ticket, users[ticket.UserId]))
            .ToArray();
        return new PagedResult<SupportTicketDto>(items, page, pageSize, total);
    }

    private async Task<SupportTicketDto> MapAsync(SupportTicket ticket, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().SingleAsync(x => x.Id == ticket.UserId, cancellationToken);
        return Map(ticket, user);
    }

    private static SupportTicketDto Map(SupportTicket ticket, ApplicationUser user) => new(
        ticket.Id,
        ticket.UserId,
        user.FullName,
        user.Email ?? string.Empty,
        ticket.Category,
        ticket.Subject,
        ticket.Description,
        ticket.Status,
        ticket.AdminReply,
        ticket.CreatedAtUtc,
        ticket.UpdatedAtUtc,
        ticket.ResolvedAtUtc);

    private static void Validate(CreateSupportTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || request.Category.Trim().Length > 80)
            throw new ValidationException("Choose a valid help category.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 160)
            throw new ValidationException("Subject is required and must be at most 160 characters.");
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 3000)
            throw new ValidationException("Description is required and must be at most 3000 characters.");
    }

    private void EnsureReviewer()
    {
        if (!currentUser.IsInRole(AppRoles.Admin) && !currentUser.IsInRole(AppRoles.Moderator))
            throw new ForbiddenException("Only authorised support reviewers can perform this action.");
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
