using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class TenancyService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IChatService chatService,
    IndiaDateProvider dateProvider)
    : ITenancyService
{
    public async Task<TenancyDto> ConfirmAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);

        if (tenancy.TenantUserId != currentUser.UserId)
            throw new ForbiddenException("Only the selected tenant can confirm this tenancy.");

        if (tenancy.Status != TenancyStatus.PendingTenantConfirmation)
            throw new ConflictException("The tenancy is not waiting for tenant confirmation.");

        var today = dateProvider.Today;
        if (tenancy.StartDate < today)
            throw new ConflictException("The proposed move-in date has passed. Ask the owner to accept a new application with an updated date.");

        tenancy.Status = TenancyStatus.Active;
        tenancy.Property.Status = PropertyStatus.Occupied;

        var acceptedApplication = await dbContext.RentalApplications
            .SingleOrDefaultAsync(x => x.TenancyId == tenancy.Id, cancellationToken);

        if (acceptedApplication is not null)
        {
            acceptedApplication.Status = RentalApplicationStatus.Accepted;
            acceptedApplication.DecisionAtUtc ??= DateTimeOffset.UtcNow;
        }

        var otherApplications = await dbContext.RentalApplications
            .Where(x => x.PropertyId == tenancy.PropertyId &&
                        x.TenancyId != tenancy.Id &&
                        (x.Status == RentalApplicationStatus.Submitted ||
                         x.Status == RentalApplicationStatus.Shortlisted))
            .ToListAsync(cancellationToken);

        foreach (var application in otherApplications)
        {
            application.Status = RentalApplicationStatus.Closed;
            application.DecisionAtUtc = DateTimeOffset.UtcNow;
            application.DecisionByUserId = tenancy.OwnerUserId;
            application.DecisionReason = "Property is no longer available.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var application in otherApplications)
        {
            await chatService.AddSystemMessageAsync(
                tenancy.PropertyId,
                tenancy.OwnerUserId,
                application.TenantUserId,
                "The application was closed because the property is now occupied.",
                cancellationToken);
        }

        await chatService.AttachTenancyAsync(
            tenancy.PropertyId,
            tenancy.OwnerUserId,
            tenancy.TenantUserId,
            tenancy.Id,
            cancellationToken);
        await chatService.AddSystemMessageAsync(
            tenancy.PropertyId,
            tenancy.OwnerUserId,
            tenancy.TenantUserId,
            $"The tenant confirmed the tenancy. Move-in date: {tenancy.StartDate:dd MMM yyyy}.",
            cancellationToken);

        return await MapAsync(tenancy, tenancy.Property.Title, cancellationToken);
    }

    public async Task<TenancyDto> CancelPendingAsync(
        Guid tenancyId,
        CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);
        EnsureParty(tenancy);

        if (tenancy.Status != TenancyStatus.PendingTenantConfirmation)
            throw new ConflictException("Only a pending tenancy can be cancelled.");

        tenancy.Status = TenancyStatus.Cancelled;
        if (tenancy.Property.Status == PropertyStatus.Reserved)
            tenancy.Property.Status = PropertyStatus.Published;

        var application = await dbContext.RentalApplications
            .SingleOrDefaultAsync(x => x.TenancyId == tenancy.Id, cancellationToken);

        if (application is not null)
        {
            application.Status = currentUser.UserId == tenancy.TenantUserId
                ? RentalApplicationStatus.Withdrawn
                : RentalApplicationStatus.Rejected;
            application.DecisionAtUtc = DateTimeOffset.UtcNow;
            application.DecisionByUserId = currentUser.UserId;
            application.DecisionReason = currentUser.UserId == tenancy.TenantUserId
                ? "Tenant declined the tenancy invitation."
                : "Owner cancelled the pending tenancy invitation.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await chatService.AddSystemMessageAsync(
            tenancy.PropertyId,
            tenancy.OwnerUserId,
            tenancy.TenantUserId,
            currentUser.UserId == tenancy.TenantUserId
                ? "The tenant declined the tenancy invitation."
                : "The owner cancelled the tenancy invitation.",
            cancellationToken);

        return await MapAsync(tenancy, tenancy.Property.Title, cancellationToken);
    }

    public async Task<TenancyDto> RequestEndAsync(
        Guid tenancyId,
        RequestTenancyEndRequest request,
        CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);
        EnsureParty(tenancy);

        if (tenancy.Status != TenancyStatus.Active)
            throw new ConflictException("Only an active tenancy can receive a closure request.");

        var today = dateProvider.Today;
        if (request.RequestedEndDate < today)
            throw new ValidationException("Requested move-out date cannot be in the past.");
        if (request.RequestedEndDate < tenancy.StartDate)
            throw new ValidationException("Requested move-out date cannot be before the tenancy start date.");

        var reason = NormalizeReason(request.Reason);
        if (reason is null)
            throw new ValidationException("A reason is required for the tenancy closure request.");

        tenancy.Status = TenancyStatus.EndRequested;
        tenancy.EndRequestedByUserId = currentUser.UserId;
        tenancy.EndRequestedAtUtc = DateTimeOffset.UtcNow;
        tenancy.RequestedEndDate = request.RequestedEndDate;
        tenancy.EndRequestReason = reason;
        tenancy.EndApprovedByUserId = null;
        tenancy.EndApprovedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await chatService.AddSystemMessageAsync(
            tenancy.PropertyId,
            tenancy.OwnerUserId,
            tenancy.TenantUserId,
            $"A tenancy closure was requested for {request.RequestedEndDate:dd MMM yyyy}. Reason: {reason}",
            cancellationToken);

        return await MapAsync(tenancy, tenancy.Property.Title, cancellationToken);
    }

    public async Task<TenancyDto> CancelEndRequestAsync(
        Guid tenancyId,
        CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);
        EnsureParty(tenancy);

        if (tenancy.Status != TenancyStatus.EndRequested)
            throw new ConflictException("There is no unapproved tenancy closure request to cancel.");

        if (tenancy.EndRequestedByUserId != currentUser.UserId)
            throw new ForbiddenException("Only the person who requested closure can cancel it.");

        tenancy.Status = TenancyStatus.Active;
        ClearEndRequest(tenancy);
        await dbContext.SaveChangesAsync(cancellationToken);

        await chatService.AddSystemMessageAsync(
            tenancy.PropertyId,
            tenancy.OwnerUserId,
            tenancy.TenantUserId,
            "The tenancy closure request was withdrawn.",
            cancellationToken);

        return await MapAsync(tenancy, tenancy.Property.Title, cancellationToken);
    }

    public async Task<TenancyDto> ConfirmEndAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);
        EnsureParty(tenancy);

        if (tenancy.Status != TenancyStatus.EndRequested)
            throw new ConflictException("No tenancy closure request is waiting for approval.");

        if (tenancy.EndRequestedByUserId == currentUser.UserId)
            throw new ConflictException("The other party must approve the tenancy closure request.");

        if (tenancy.RequestedEndDate is null)
            throw new ConflictException("The tenancy closure request does not contain a move-out date.");

        tenancy.EndApprovedByUserId = currentUser.UserId;
        tenancy.EndApprovedAtUtc = DateTimeOffset.UtcNow;

        var today = dateProvider.Today;
        if (tenancy.RequestedEndDate <= today)
        {
            await FinalizeEndAsync(tenancy, cancellationToken);
        }
        else
        {
            tenancy.Status = TenancyStatus.EndScheduled;
            await dbContext.SaveChangesAsync(cancellationToken);
            await chatService.AddSystemMessageAsync(
                tenancy.PropertyId,
                tenancy.OwnerUserId,
                tenancy.TenantUserId,
                $"Both parties approved the tenancy closure for {tenancy.RequestedEndDate:dd MMM yyyy}.",
                cancellationToken);
        }

        return await MapAsync(tenancy, tenancy.Property.Title, cancellationToken);
    }

    public async Task<TenancyDto> CompleteEndAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);
        EnsureParty(tenancy);

        if (tenancy.Status != TenancyStatus.EndScheduled)
            throw new ConflictException("This tenancy does not have an approved scheduled closure.");

        if (tenancy.RequestedEndDate is null)
            throw new ConflictException("The approved closure date is missing.");

        var today = dateProvider.Today;
        if (tenancy.RequestedEndDate > today)
            throw new ConflictException($"The tenancy can be completed on or after {tenancy.RequestedEndDate:dd MMM yyyy}.");

        await FinalizeEndAsync(tenancy, cancellationToken);
        return await MapAsync(tenancy, tenancy.Property.Title, cancellationToken);
    }

    public async Task<PagedResult<TenancyDto>> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Tenancies.AsNoTracking()
            .Include(x => x.Property)
            .Where(x => x.OwnerUserId == currentUser.UserId || x.TenantUserId == currentUser.UserId)
            .OrderByDescending(x => x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var entities = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<TenancyDto>(entities.Count);
        foreach (var entity in entities)
            items.Add(await MapAsync(entity, entity.Property.Title, cancellationToken));

        return new PagedResult<TenancyDto>(items, page, pageSize, total);
    }

    private async Task FinalizeEndAsync(Tenancy tenancy, CancellationToken cancellationToken)
    {
        tenancy.Status = TenancyStatus.Ended;
        tenancy.ActualEndDate = tenancy.RequestedEndDate ?? dateProvider.Today;
        tenancy.Property.Status = PropertyStatus.Published;

        var application = await dbContext.RentalApplications
            .SingleOrDefaultAsync(x => x.TenancyId == tenancy.Id, cancellationToken);
        if (application is not null)
        {
            application.Status = RentalApplicationStatus.Closed;
            application.DecisionAtUtc = DateTimeOffset.UtcNow;
            application.DecisionByUserId = currentUser.UserId;
            application.DecisionReason = "Tenancy completed.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await chatService.AddSystemMessageAsync(
            tenancy.PropertyId,
            tenancy.OwnerUserId,
            tenancy.TenantUserId,
            $"The tenancy was completed on {tenancy.ActualEndDate:dd MMM yyyy}. Reviews are now available.",
            cancellationToken);
    }

    private async Task<Tenancy> GetWithPropertyAsync(
        Guid tenancyId,
        CancellationToken cancellationToken) =>
        await dbContext.Tenancies
            .Include(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == tenancyId, cancellationToken)
        ?? throw new NotFoundException("Tenancy was not found.");

    private void EnsureParty(Tenancy tenancy)
    {
        if (tenancy.OwnerUserId != currentUser.UserId &&
            tenancy.TenantUserId != currentUser.UserId)
        {
            throw new ForbiddenException("You are not a party to this tenancy.");
        }
    }

    private static void ClearEndRequest(Tenancy tenancy)
    {
        tenancy.EndRequestedByUserId = null;
        tenancy.EndRequestedAtUtc = null;
        tenancy.RequestedEndDate = null;
        tenancy.EndRequestReason = null;
        tenancy.EndApprovedByUserId = null;
        tenancy.EndApprovedAtUtc = null;
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var value = reason.Trim();
        if (value.Length > 500)
            throw new ValidationException("Reason must be at most 500 characters.");

        return value;
    }

    private async Task<TenancyDto> MapAsync(
        Tenancy x,
        string propertyTitle,
        CancellationToken cancellationToken)
    {
        var conversationId = await dbContext.ChatConversations.AsNoTracking()
            .Where(conversation => conversation.PropertyId == x.PropertyId &&
                                   conversation.OwnerUserId == x.OwnerUserId &&
                                   conversation.TenantUserId == x.TenantUserId)
            .Select(conversation => (Guid?)conversation.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return new TenancyDto(
            x.Id,
            x.PropertyId,
            propertyTitle,
            x.OwnerUserId,
            x.TenantUserId,
            x.StartDate,
            x.ExpectedEndDate,
            x.ActualEndDate,
            x.AgreedMonthlyRent,
            x.AgreedSecurityDeposit,
            x.Status,
            x.EndRequestedByUserId,
            x.EndRequestedAtUtc,
            x.RequestedEndDate,
            x.EndRequestReason,
            x.EndApprovedByUserId,
            x.EndApprovedAtUtc,
            conversationId,
            x.CreatedAtUtc);
    }

}
