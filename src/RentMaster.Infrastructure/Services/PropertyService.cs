using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class PropertyService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IIdentityVerificationService verificationService)
    : IPropertyService
{
    public async Task<OwnerPropertyDto> CreateAsync(
        CreatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        await EnsureVerifiedAsync(cancellationToken);
        Validate(request);

        var property = new Property
        {
            OwnerUserId = currentUser.UserId,
            Title = request.Title.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            Locality = request.Locality.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            PostalCode = request.PostalCode.Trim(),
            MonthlyRent = request.MonthlyRent,
            SecurityDeposit = request.SecurityDeposit,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            Status = request.Publish ? PropertyStatus.Published : PropertyStatus.Draft
        };

        dbContext.Properties.Add(property);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapOwner(property);
    }

    public async Task<OwnerPropertyDto> UpdateAsync(
        Guid id,
        UpdatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        Validate(request);

        var property = await dbContext.Properties
            .SingleOrDefaultAsync(
                x => x.Id == id && x.OwnerUserId == currentUser.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Property was not found.");

        if (!Enum.IsDefined(request.Status))
            throw new ValidationException("Property status is invalid.");

        var requestedSystemStatus = request.Status is PropertyStatus.Occupied or PropertyStatus.Reserved;
        var currentSystemStatus = property.Status is PropertyStatus.Occupied or PropertyStatus.Reserved;
        if (requestedSystemStatus && request.Status != property.Status)
            throw new ConflictException("Reserved and occupied statuses are managed only by the tenancy workflow.");

        if (currentSystemStatus && request.Status != property.Status)
            throw new ConflictException("A reserved or occupied property status is managed by the tenancy workflow.");

        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new ValidationException("RowVersion is required.");

        byte[] rowVersion;
        try
        {
            rowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException("RowVersion is invalid.");
        }

        if (rowVersion.Length != 8)
            throw new ValidationException("RowVersion is invalid.");

        dbContext.Entry(property).Property(x => x.RowVersion).OriginalValue = rowVersion;

        property.Title = request.Title.Trim();
        property.AddressLine1 = request.AddressLine1.Trim();
        property.Locality = request.Locality.Trim();
        property.City = request.City.Trim();
        property.State = request.State.Trim();
        property.PostalCode = request.PostalCode.Trim();
        property.MonthlyRent = request.MonthlyRent;
        property.SecurityDeposit = request.SecurityDeposit;
        property.Bedrooms = request.Bedrooms;
        property.Bathrooms = request.Bathrooms;
        property.Status = request.Status;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The property was modified by another request. Reload it and retry.");
        }

        return MapOwner(property);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureOwner();

        var property = await dbContext.Properties
            .SingleOrDefaultAsync(
                x => x.Id == id && x.OwnerUserId == currentUser.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Property was not found.");

        var hasOpenTenancy = await dbContext.Tenancies.AnyAsync(
            x => x.PropertyId == id &&
                 x.Status != TenancyStatus.Ended &&
                 x.Status != TenancyStatus.Cancelled,
            cancellationToken);

        if (hasOpenTenancy)
            throw new ConflictException("A property with an open tenancy cannot be deleted.");

        dbContext.Properties.Remove(property);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<OwnerPropertyDto>> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = dbContext.Properties.AsNoTracking()
            .Where(x => x.OwnerUserId == currentUser.UserId)
            .OrderByDescending(x => x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var entities = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OwnerPropertyDto>(
            entities.Select(MapOwner).ToArray(),
            page,
            pageSize,
            total);
    }

    public async Task<PagedResult<PublicPropertyDto>> SearchAsync(
        PropertySearchRequest request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = NormalizePage(request.Page, request.PageSize);

        var propertyQuery = dbContext.Properties.AsNoTracking()
            .Where(x => x.Status == PropertyStatus.Published);

        if (!string.IsNullOrWhiteSpace(request.City))
            propertyQuery = propertyQuery.Where(x => x.City == request.City.Trim());

        if (!string.IsNullOrWhiteSpace(request.Locality))
            propertyQuery = propertyQuery.Where(x => x.Locality.Contains(request.Locality.Trim()));

        if (request.MaximumRent.HasValue)
            propertyQuery = propertyQuery.Where(x => x.MonthlyRent <= request.MaximumRent.Value);

        if (request.MinimumBedrooms.HasValue)
            propertyQuery = propertyQuery.Where(x => x.Bedrooms >= request.MinimumBedrooms.Value);

        var query =
            from property in propertyQuery
            join owner in dbContext.Users.AsNoTracking()
                on property.OwnerUserId equals owner.Id
            orderby property.CreatedAtUtc descending
            select new { property, owner.PublicProfileCode };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PublicPropertyDto(
                x.property.Id,
                x.PublicProfileCode,
                x.property.Title,
                x.property.Locality,
                x.property.City,
                x.property.State,
                x.property.PostalCode,
                x.property.MonthlyRent,
                x.property.SecurityDeposit,
                x.property.Bedrooms,
                x.property.Bathrooms))
            .ToListAsync(cancellationToken);

        return new PagedResult<PublicPropertyDto>(items, page, pageSize, total);
    }

    public async Task<PublicPropertyDetailsDto> GetPublicDetailsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var canViewThroughApplication = currentUser.IsInRole(AppRoles.Tenant) &&
            await dbContext.RentalApplications.AsNoTracking().AnyAsync(
                x => x.PropertyId == id && x.TenantUserId == currentUser.UserId,
                cancellationToken);
        var canViewThroughTenancy = await dbContext.Tenancies.AsNoTracking().AnyAsync(
            x => x.PropertyId == id &&
                 (x.OwnerUserId == currentUser.UserId || x.TenantUserId == currentUser.UserId),
            cancellationToken);

        var result = await (
            from property in dbContext.Properties.AsNoTracking()
            join owner in dbContext.Users.AsNoTracking()
                on property.OwnerUserId equals owner.Id
            where property.Id == id &&
                  (property.Status == PropertyStatus.Published ||
                   property.OwnerUserId == currentUser.UserId ||
                   canViewThroughApplication ||
                   canViewThroughTenancy)
            select new { property, owner.PublicProfileCode })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Property was not found or is not available to this account.");

        var hasActiveApplication = currentUser.IsInRole(AppRoles.Tenant) &&
            await dbContext.RentalApplications.AsNoTracking().AnyAsync(
                x => x.PropertyId == id &&
                     x.TenantUserId == currentUser.UserId &&
                     (x.Status == RentalApplicationStatus.Submitted ||
                      x.Status == RentalApplicationStatus.Shortlisted ||
                      x.Status == RentalApplicationStatus.Accepted),
                cancellationToken);

        return new PublicPropertyDetailsDto(
            result.property.Id,
            result.PublicProfileCode,
            result.property.Title,
            result.property.Locality,
            result.property.City,
            result.property.State,
            result.property.PostalCode,
            result.property.MonthlyRent,
            result.property.SecurityDeposit,
            result.property.Bedrooms,
            result.property.Bathrooms,
            result.property.Status,
            hasActiveApplication);
    }

    private void EnsureOwner()
    {
        if (!currentUser.IsInRole(AppRoles.Owner))
            throw new ForbiddenException("Only owners can perform this operation.");
    }

    private async Task EnsureVerifiedAsync(CancellationToken cancellationToken)
    {
        if (!await verificationService.IsUserVerifiedAsync(currentUser.UserId, cancellationToken))
            throw new ForbiddenException("Identity verification must be completed first.");
    }

    private static void Validate(CreatePropertyRequest request)
    {
        ValidateValues(
            request.Title,
            request.AddressLine1,
            request.Locality,
            request.City,
            request.State,
            request.PostalCode,
            request.MonthlyRent,
            request.SecurityDeposit,
            request.Bedrooms,
            request.Bathrooms);
    }

    private static void Validate(UpdatePropertyRequest request)
    {
        ValidateValues(
            request.Title,
            request.AddressLine1,
            request.Locality,
            request.City,
            request.State,
            request.PostalCode,
            request.MonthlyRent,
            request.SecurityDeposit,
            request.Bedrooms,
            request.Bathrooms);
    }

    private static void ValidateValues(
        string title,
        string address,
        string locality,
        string city,
        string state,
        string postalCode,
        decimal monthlyRent,
        decimal deposit,
        int bedrooms,
        int bathrooms)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
            throw new ValidationException("Title is required and must be at most 160 characters.");

        if (string.IsNullOrWhiteSpace(address) ||
            string.IsNullOrWhiteSpace(locality) ||
            string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ValidationException("Complete property address is required.");
        }

        if (address.Trim().Length > 250 ||
            locality.Trim().Length > 120 ||
            city.Trim().Length > 120 ||
            state.Trim().Length > 120 ||
            postalCode.Trim().Length > 12)
        {
            throw new ValidationException("One or more property address fields exceed the allowed length.");
        }

        if (monthlyRent <= 0 || deposit < 0)
            throw new ValidationException("Rent must be positive and deposit cannot be negative.");

        if (bedrooms is < 0 or > 20 || bathrooms is < 0 or > 20)
            throw new ValidationException("Bedroom or bathroom count is invalid.");
    }

    private static OwnerPropertyDto MapOwner(Property x) =>
        new(
            x.Id,
            x.Title,
            x.AddressLine1,
            x.Locality,
            x.City,
            x.State,
            x.PostalCode,
            x.MonthlyRent,
            x.SecurityDeposit,
            x.Bedrooms,
            x.Bathrooms,
            x.Status,
            Convert.ToBase64String(x.RowVersion));

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
