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
    ICurrentUserService currentUser)
    : ITenancyService
{
    public async Task<TenancyDto> ConfirmAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);

        if (tenancy.TenantUserId != currentUser.UserId)
            throw new ForbiddenException("Only the selected tenant can confirm this tenancy.");

        if (tenancy.Status != TenancyStatus.PendingTenantConfirmation)
            throw new ConflictException("The tenancy is not waiting for tenant confirmation.");

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
        return Map(tenancy, tenancy.Property.Title);
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
        return Map(tenancy, tenancy.Property.Title);
    }

    public async Task<TenancyDto> RequestEndAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);
        EnsureParty(tenancy);

        if (tenancy.Status != TenancyStatus.Active)
            throw new ConflictException("Only an active tenancy can be ended.");

        tenancy.Status = TenancyStatus.EndRequested;
        tenancy.EndRequestedByUserId = currentUser.UserId;
        tenancy.EndRequestedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(tenancy, tenancy.Property.Title);
    }

    public async Task<TenancyDto> ConfirmEndAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);
        EnsureParty(tenancy);

        if (tenancy.Status != TenancyStatus.EndRequested)
            throw new ConflictException("No tenancy-end request is pending.");

        if (tenancy.EndRequestedByUserId == currentUser.UserId)
            throw new ConflictException("The other party must confirm the tenancy end.");

        tenancy.Status = TenancyStatus.Ended;
        tenancy.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
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
        return Map(tenancy, tenancy.Property.Title);
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

        return new PagedResult<TenancyDto>(
            entities.Select(x => Map(x, x.Property.Title)).ToArray(),
            page,
            pageSize,
            total);
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

    private static TenancyDto Map(Tenancy x, string propertyTitle) =>
        new(
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
            x.CreatedAtUtc);
}
