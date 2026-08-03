using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    IIdentityVerificationService verificationService,
    IChatService chatService,
    IDocumentStorage documentStorage,
    ILogger<PropertyService> logger)
    : IPropertyService
{
    public async Task<OwnerPropertyDto> CreateAsync(
        CreatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        await EnsureVerifiedAsync(cancellationToken);
        Validate(request);

        if (request.Publish)
            throw new ValidationException("Save the property as a draft, attach at least one photo, and then publish it.");

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
            Status = PropertyStatus.Draft
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
        await EnsureVerifiedAsync(cancellationToken);
        Validate(request);

        var property = await dbContext.Properties
            .Include(x => x.Photos)
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

        if (request.Status == PropertyStatus.Published && property.Photos.Count == 0)
            throw new ValidationException("Attach at least one property photo before publishing the listing.");

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

        var wasPublished = property.Status == PropertyStatus.Published;
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

        var unavailableTenantIds = wasPublished && request.Status != PropertyStatus.Published
            ? await CloseOpenApplicationsAsync(property.Id, cancellationToken)
            : [];

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The property was modified by another request. Reload it and retry.");
        }

        await NotifyPropertyUnavailableAsync(property, unavailableTenantIds, cancellationToken);
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

        var wasAvailable = property.Status == PropertyStatus.Published;
        property.Status = PropertyStatus.Inactive;
        var unavailableTenantIds = wasAvailable
            ? await CloseOpenApplicationsAsync(property.Id, cancellationToken)
            : [];

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyPropertyUnavailableAsync(property, unavailableTenantIds, cancellationToken);
    }

    public async Task<PagedResult<OwnerPropertyDto>> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = dbContext.Properties.AsNoTracking()
            .Include(x => x.Photos)
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
            .Where(x => x.Status == PropertyStatus.Published &&
                        x.Photos.Any() &&
                        x.MonthlyRent >= 500 &&
                        x.Bedrooms >= 1 && x.Bathrooms >= 1 &&
                        x.Title != string.Empty && x.Locality != string.Empty && x.City != string.Empty);

        if (!string.IsNullOrWhiteSpace(request.City))
            propertyQuery = propertyQuery.Where(x => x.City == request.City.Trim());

        if (!string.IsNullOrWhiteSpace(request.Locality))
            propertyQuery = propertyQuery.Where(x => x.Locality.Contains(request.Locality.Trim()));

        if (request.MaximumRent.HasValue)
        {
            if (request.MaximumRent.Value <= 0)
                throw new ValidationException("Maximum rent must be greater than zero.");
            propertyQuery = propertyQuery.Where(x => x.MonthlyRent <= request.MaximumRent.Value);
        }

        if (request.MinimumBedrooms.HasValue)
        {
            if (request.MinimumBedrooms.Value is < 1 or > 20)
                throw new ValidationException("Minimum bedrooms must be between 1 and 20.");
            propertyQuery = propertyQuery.Where(x => x.Bedrooms >= request.MinimumBedrooms.Value);
        }

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
                x.property.Bathrooms,
                x.property.Photos.OrderBy(photo => photo.SortOrder).Select(photo => (Guid?)photo.Id).FirstOrDefault()))
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
            hasActiveApplication,
            await dbContext.PropertyPhotos.AsNoTracking()
                .Where(photo => photo.PropertyId == id)
                .OrderBy(photo => photo.SortOrder)
                .Select(photo => new PropertyPhotoDto(
                    photo.Id,
                    photo.OriginalFileName,
                    photo.ContentType,
                    photo.SizeBytes,
                    photo.SortOrder))
                .ToListAsync(cancellationToken));
    }

    public async Task<PropertyPhotoDto> AddPhotoAsync(
        Guid propertyId,
        UploadPropertyPhotoCommand command,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        await EnsureVerifiedAsync(cancellationToken);
        var property = await dbContext.Properties
            .Include(x => x.Photos)
            .SingleOrDefaultAsync(
                x => x.Id == propertyId && x.OwnerUserId == currentUser.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Property was not found.");

        if (property.Photos.Count >= 10)
            throw new ConflictException("A property can contain up to 10 photos.");

        await ValidatePhotoAsync(command, cancellationToken);
        var objectName = await documentStorage.SaveAsync(
            currentUser.UserId,
            command.FileName,
            command.ContentType,
            command.Content,
            cancellationToken);

        try
        {
            var photo = new PropertyPhoto
            {
                PropertyId = property.Id,
                StorageObjectName = objectName,
                OriginalFileName = Path.GetFileName(command.FileName),
                ContentType = command.ContentType,
                SizeBytes = command.SizeBytes,
                SortOrder = property.Photos.Count
            };
            dbContext.PropertyPhotos.Add(photo);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new PropertyPhotoDto(photo.Id, photo.OriginalFileName, photo.ContentType, photo.SizeBytes, photo.SortOrder);
        }
        catch
        {
            await documentStorage.DeleteIfExistsAsync(objectName, CancellationToken.None);
            throw;
        }
    }

    public async Task<StoredPropertyPhoto> OpenPhotoAsync(
        Guid propertyId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        var photo = await dbContext.PropertyPhotos.AsNoTracking()
            .Include(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == photoId && x.PropertyId == propertyId, cancellationToken)
            ?? throw new NotFoundException("Property photo was not found.");

        var canView = photo.Property.Status == PropertyStatus.Published ||
                      photo.Property.OwnerUserId == currentUser.UserId ||
                      await dbContext.RentalApplications.AsNoTracking().AnyAsync(
                          x => x.PropertyId == propertyId && x.TenantUserId == currentUser.UserId,
                          cancellationToken) ||
                      await dbContext.Tenancies.AsNoTracking().AnyAsync(
                          x => x.PropertyId == propertyId &&
                               (x.OwnerUserId == currentUser.UserId || x.TenantUserId == currentUser.UserId),
                          cancellationToken);
        if (!canView)
            throw new ForbiddenException("You cannot view this property photo.");

        var stream = await documentStorage.OpenReadAsync(photo.StorageObjectName, cancellationToken);
        return new StoredPropertyPhoto(stream, photo.ContentType, photo.OriginalFileName);
    }

    public async Task DeletePhotoAsync(
        Guid propertyId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        var photo = await dbContext.PropertyPhotos
            .Include(x => x.Property)
            .SingleOrDefaultAsync(
                x => x.Id == photoId && x.PropertyId == propertyId && x.Property.OwnerUserId == currentUser.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Property photo was not found.");

        var photoCount = await dbContext.PropertyPhotos.CountAsync(
            x => x.PropertyId == propertyId,
            cancellationToken);
        if (photoCount <= 1 && photo.Property.Status is PropertyStatus.Published or PropertyStatus.Reserved or PropertyStatus.Occupied)
            throw new ConflictException("Keep at least one photo while the property is published, reserved or occupied.");

        dbContext.PropertyPhotos.Remove(photo);
        await dbContext.SaveChangesAsync(cancellationToken);
        await documentStorage.DeleteIfExistsAsync(photo.StorageObjectName, cancellationToken);
    }

    private static async Task ValidatePhotoAsync(
        UploadPropertyPhotoCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SizeBytes <= 0 || command.SizeBytes > 8 * 1024 * 1024)
            throw new ValidationException("Each property photo must be smaller than 8 MB.");
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        var allowedExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedTypes.Contains(command.ContentType) || !allowedExtensions.Contains(Path.GetExtension(command.FileName).ToLowerInvariant()))
            throw new ValidationException("Choose JPG, PNG or WEBP property photos.");
        if (!command.Content.CanSeek)
            throw new ValidationException("The selected photo cannot be read.");

        var header = new byte[12];
        var bytesRead = await command.Content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        command.Content.Position = 0;
        var jpeg = bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var png = bytesRead >= 8 && header.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var webp = bytesRead >= 12 &&
                   Encoding.ASCII.GetString(header, 0, 4) == "RIFF" &&
                   Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
        var valid = command.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => jpeg,
            "image/png" => png,
            "image/webp" => webp,
            _ => false
        };
        if (!valid)
            throw new ValidationException("The selected photo does not match its file type.");
    }

    private async Task<string[]> CloseOpenApplicationsAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var applications = await dbContext.RentalApplications
            .Where(application => application.PropertyId == propertyId &&
                                  (application.Status == RentalApplicationStatus.Submitted ||
                                   application.Status == RentalApplicationStatus.Shortlisted))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var application in applications)
        {
            application.Status = RentalApplicationStatus.Closed;
            application.DecisionAtUtc = now;
            application.DecisionByUserId = currentUser.UserId;
            application.DecisionReason = "Property is no longer available.";
        }

        var conversationTenantIds = await dbContext.ChatConversations.AsNoTracking()
            .Where(conversation => conversation.PropertyId == propertyId)
            .Select(conversation => conversation.TenantUserId)
            .ToListAsync(cancellationToken);

        return applications.Select(application => application.TenantUserId)
            .Concat(conversationTenantIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task NotifyPropertyUnavailableAsync(
        Property property,
        IReadOnlyCollection<string> tenantUserIds,
        CancellationToken cancellationToken)
    {
        foreach (var tenantUserId in tenantUserIds)
        {
            try
            {
                await chatService.AddSystemMessageAsync(
                    property.Id,
                    property.OwnerUserId,
                    tenantUserId,
                    "The owner made this property unavailable. Any open application for this property was closed.",
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception notificationException)
            {
                logger.LogWarning(
                    notificationException,
                    "Property {PropertyId} was made unavailable, but the workflow message could not be written for tenant {TenantUserId}.",
                    property.Id,
                    tenantUserId);
            }
        }
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

        if (monthlyRent < 500 || monthlyRent > 100_000_000 || deposit < 0 || deposit > 500_000_000)
            throw new ValidationException("Enter a monthly rent of at least ₹500 and valid non-negative deposit.");

        if (bedrooms is < 1 or > 20 || bathrooms is < 1 or > 20)
            throw new ValidationException("Bedrooms and bathrooms must each be between 1 and 20.");

        var normalizedPostalCode = postalCode.Trim();
        if (normalizedPostalCode.Length != 6 ||
            normalizedPostalCode[0] == '0' ||
            normalizedPostalCode.Any(character => !char.IsDigit(character)))
        {
            throw new ValidationException("Enter a valid 6-digit Indian postal code.");
        }
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
            x.Photos.OrderBy(photo => photo.SortOrder).Select(photo => (Guid?)photo.Id).FirstOrDefault(),
            x.Photos.Count,
            Convert.ToBase64String(x.RowVersion));

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
