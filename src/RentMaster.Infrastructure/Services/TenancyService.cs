using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class TenancyService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser,
    IIdentityVerificationService verificationService)
    : ITenancyService
{
    public async Task<TenancyDto> CreateAsync(
        CreateTenancyRequest request,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        await EnsureVerifiedAsync(currentUser.UserId, cancellationToken);

        var property = await dbContext.Properties
            .SingleOrDefaultAsync(
                x => x.Id == request.PropertyId && x.OwnerUserId == currentUser.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Property was not found.");

        if (property.Status != PropertyStatus.Published)
            throw new ConflictException("Only a published and available property can start a tenancy.");

        var tenant = await userManager.FindByEmailAsync(request.TenantEmail.Trim().ToLowerInvariant())
            ?? throw new NotFoundException("Tenant account was not found.");

        if (!await userManager.IsInRoleAsync(tenant, AppRoles.Tenant))
            throw new ValidationException("The selected account is not a tenant.");

        await EnsureVerifiedAsync(tenant.Id, cancellationToken);

        if (request.StartDate < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            throw new ValidationException("Start date cannot be in the past.");

        if (request.ExpectedEndDate.HasValue &&
            request.ExpectedEndDate.Value <= request.StartDate)
        {
            throw new ValidationException("Expected end date must be after the start date.");
        }

        if (request.AgreedMonthlyRent <= 0 || request.AgreedSecurityDeposit < 0)
            throw new ValidationException("Agreed rent or deposit is invalid.");

        var hasOpenTenancy = await dbContext.Tenancies.AnyAsync(
            x => x.PropertyId == property.Id &&
                 x.Status != TenancyStatus.Ended &&
                 x.Status != TenancyStatus.Cancelled,
            cancellationToken);

        if (hasOpenTenancy)
            throw new ConflictException("The property already has an open tenancy.");

        var tenancy = new Tenancy
        {
            PropertyId = property.Id,
            OwnerUserId = currentUser.UserId,
            TenantUserId = tenant.Id,
            StartDate = request.StartDate,
            ExpectedEndDate = request.ExpectedEndDate,
            AgreedMonthlyRent = request.AgreedMonthlyRent,
            AgreedSecurityDeposit = request.AgreedSecurityDeposit
        };

        dbContext.Tenancies.Add(tenancy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(tenancy, property.Title);
    }

    public async Task<TenancyDto> ConfirmAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var tenancy = await GetWithPropertyAsync(tenancyId, cancellationToken);

        if (tenancy.TenantUserId != currentUser.UserId)
            throw new ForbiddenException("Only the invited tenant can confirm this tenancy.");

        if (tenancy.Status != TenancyStatus.PendingTenantConfirmation)
            throw new ConflictException("The tenancy is not waiting for tenant confirmation.");

        tenancy.Status = TenancyStatus.Active;
        tenancy.Property.Status = PropertyStatus.Occupied;

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

    private void EnsureOwner()
    {
        if (!currentUser.IsInRole(AppRoles.Owner))
            throw new ForbiddenException("Only owners can create tenancy invitations.");
    }

    private void EnsureParty(Tenancy tenancy)
    {
        if (tenancy.OwnerUserId != currentUser.UserId &&
            tenancy.TenantUserId != currentUser.UserId)
        {
            throw new ForbiddenException("You are not a party to this tenancy.");
        }
    }

    private async Task EnsureVerifiedAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await verificationService.IsUserVerifiedAsync(userId, cancellationToken))
            throw new ForbiddenException("Both owner and tenant must complete identity verification.");
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
