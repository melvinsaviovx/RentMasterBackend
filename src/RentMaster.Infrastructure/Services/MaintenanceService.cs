using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Persistence;
using RentMaster.Infrastructure.Security;

namespace RentMaster.Infrastructure.Services;

public sealed class MaintenanceService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    UserManager<ApplicationUser> userManager) : IMaintenanceService
{
    public async Task<MaintenanceRequestDto> CreateAsync(
        CreateMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Owner) && !currentUser.IsInRole(AppRoles.Tenant))
            throw new ForbiddenException("Only an owner or tenant can raise a maintenance request.");

        ValidateRequest(request);

        var tenancy = await dbContext.Tenancies
            .Include(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == request.TenancyId, cancellationToken)
            ?? throw new NotFoundException("Tenancy was not found.");

        if (tenancy.OwnerUserId != currentUser.UserId && tenancy.TenantUserId != currentUser.UserId)
            throw new ForbiddenException("This tenancy does not belong to your account.");

        if (tenancy.Status is TenancyStatus.Ended or TenancyStatus.Cancelled)
            throw new ConflictException("Maintenance cannot be raised for an ended or cancelled tenancy.");

        var item = new MaintenanceRequest
        {
            TenancyId = tenancy.Id,
            PropertyId = tenancy.PropertyId,
            CreatedByUserId = currentUser.UserId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority
        };

        dbContext.MaintenanceRequests.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(item, cancellationToken);
    }

    public async Task<PagedResult<MaintenanceRequestDto>> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePage(page, pageSize);

        IQueryable<MaintenanceRequest> query = dbContext.MaintenanceRequests.AsNoTracking();

        if (currentUser.IsInRole(AppRoles.Maintenance))
        {
            query = query.Where(x => x.AssignedToUserId == currentUser.UserId);
        }
        else if (currentUser.IsInRole(AppRoles.Owner))
        {
            query = query.Where(x => x.Tenancy.OwnerUserId == currentUser.UserId);
        }
        else if (currentUser.IsInRole(AppRoles.Tenant))
        {
            query = query.Where(x => x.Tenancy.TenantUserId == currentUser.UserId);
        }
        else if (!currentUser.IsInRole(AppRoles.Admin))
        {
            throw new ForbiddenException("You do not have maintenance access.");
        }

        query = query
            .OrderBy(x => x.Status == MaintenanceRequestStatus.New ? 0 :
                          x.Status == MaintenanceRequestStatus.Assigned ? 1 :
                          x.Status == MaintenanceRequestStatus.InProgress ? 2 : 3)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var mapped = new List<MaintenanceRequestDto>(items.Count);
        foreach (var item in items)
            mapped.Add(await MapAsync(item, cancellationToken));

        return new PagedResult<MaintenanceRequestDto>(mapped, page, pageSize, total);
    }

    public async Task<IReadOnlyList<MaintenanceStaffDto>> GetStaffAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Owner) && !currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Only owners and administrators can view maintenance staff.");

        var role = await dbContext.Roles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name == AppRoles.Maintenance, cancellationToken);

        if (role is null)
            return [];

        var rows = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on userRole.UserId equals user.Id
            where userRole.RoleId == role.Id
            orderby user.FullName
            select user)
            .ToListAsync(cancellationToken);

        return rows.Select(MapStaff).ToArray();
    }

    public async Task<MaintenanceStaffDto> CreateStaffAsync(
        CreateMaintenanceStaffRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Only administrators can create maintenance accounts.");

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150)
            throw new ValidationException("Full name is required and must be at most 150 characters.");
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationException("Email is required.");
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
            throw new ValidationException("Temporary password is required.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account already exists for this email.");

        var phoneNumber = NormalizeIndianPhoneNumber(request.PhoneNumber);
        if (await dbContext.Users.AnyAsync(x => x.PhoneNumber == phoneNumber, cancellationToken))
            throw new ConflictException("An account already exists for this mobile number.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = phoneNumber,
            FullName = request.FullName.Trim(),
            LockoutEnabled = true,
            PublicProfileCode = await CreateUniquePublicProfileCodeAsync(cancellationToken)
        };

        var result = await userManager.CreateAsync(user, request.TemporaryPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(x => x.Description)));

        result = await userManager.AddToRoleAsync(user, AppRoles.Maintenance);
        if (!result.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ValidationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        return MapStaff(user);
    }

    public async Task<MaintenanceRequestDto> AssignAsync(
        Guid id,
        AssignMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MaintenanceUserId))
            throw new ValidationException("Choose a maintenance staff member.");

        var item = await dbContext.MaintenanceRequests
            .Include(x => x.Tenancy)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Maintenance request was not found.");

        if (!currentUser.IsInRole(AppRoles.Admin) &&
            (!currentUser.IsInRole(AppRoles.Owner) || item.Tenancy.OwnerUserId != currentUser.UserId))
        {
            throw new ForbiddenException("Only the property owner or an administrator can assign this request.");
        }

        if (item.Status is MaintenanceRequestStatus.Completed or MaintenanceRequestStatus.Closed)
            throw new ConflictException("Completed or closed maintenance requests cannot be reassigned.");

        var staff = await userManager.FindByIdAsync(request.MaintenanceUserId)
            ?? throw new NotFoundException("Maintenance staff account was not found.");

        if (!staff.IsActive || !await userManager.IsInRoleAsync(staff, AppRoles.Maintenance))
            throw new ValidationException("Choose an active maintenance staff account.");

        item.AssignedToUserId = staff.Id;
        item.AssignedAtUtc = DateTimeOffset.UtcNow;
        item.Status = MaintenanceRequestStatus.Assigned;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(item, cancellationToken);
    }

    public async Task<MaintenanceRequestDto> UpdateStatusAsync(
        Guid id,
        UpdateMaintenanceStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
            throw new ValidationException("Maintenance status is invalid.");
        if (request.Note?.Trim().Length > 2000)
            throw new ValidationException("Maintenance note must be at most 2000 characters.");

        var item = await dbContext.MaintenanceRequests
            .Include(x => x.Tenancy)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Maintenance request was not found.");

        if (currentUser.IsInRole(AppRoles.Maintenance))
        {
            if (item.AssignedToUserId != currentUser.UserId)
                throw new ForbiddenException("This maintenance job is not assigned to you.");
            if (request.Status is not MaintenanceRequestStatus.InProgress and not MaintenanceRequestStatus.Completed)
                throw new ValidationException("Maintenance staff can mark a job In progress or Completed.");
        }
        else if (currentUser.IsInRole(AppRoles.Owner))
        {
            if (item.Tenancy.OwnerUserId != currentUser.UserId)
                throw new ForbiddenException("This request does not belong to one of your tenancies.");
            if (request.Status != MaintenanceRequestStatus.Closed)
                throw new ValidationException("Owners can close a completed maintenance request.");
            if (item.Status != MaintenanceRequestStatus.Completed)
                throw new ConflictException("Only completed maintenance requests can be closed.");
        }
        else if (!currentUser.IsInRole(AppRoles.Admin))
        {
            throw new ForbiddenException("You do not have permission to update this maintenance request.");
        }

        var now = DateTimeOffset.UtcNow;
        item.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Note))
            item.MaintenanceNote = request.Note.Trim();

        if (request.Status == MaintenanceRequestStatus.InProgress)
            item.StartedAtUtc ??= now;
        if (request.Status == MaintenanceRequestStatus.Completed)
            item.CompletedAtUtc = now;
        if (request.Status == MaintenanceRequestStatus.Closed)
            item.ClosedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(item, cancellationToken);
    }

    private async Task<MaintenanceRequestDto> MapAsync(
        MaintenanceRequest item,
        CancellationToken cancellationToken)
    {
        var propertyTitle = await dbContext.Properties.AsNoTracking()
            .Where(x => x.Id == item.PropertyId)
            .Select(x => x.Title)
            .SingleAsync(cancellationToken);

        var userIds = new[] { item.CreatedByUserId, item.AssignedToUserId }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToArray();

        var users = await dbContext.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        return new MaintenanceRequestDto(
            item.Id,
            item.TenancyId,
            item.PropertyId,
            propertyTitle,
            item.CreatedByUserId,
            users.GetValueOrDefault(item.CreatedByUserId, "User"),
            item.AssignedToUserId,
            item.AssignedToUserId is null ? null : users.GetValueOrDefault(item.AssignedToUserId, "Maintenance"),
            item.Title,
            item.Description,
            item.Priority,
            item.Status,
            item.MaintenanceNote,
            item.CreatedAtUtc,
            item.AssignedAtUtc,
            item.StartedAtUtc,
            item.CompletedAtUtc,
            item.ClosedAtUtc);
    }

    private static MaintenanceStaffDto MapStaff(ApplicationUser user) => new(
        user.Id,
        user.FullName,
        user.Email ?? string.Empty,
        user.PhoneNumber ?? string.Empty,
        user.IsActive);

    private static void ValidateRequest(CreateMaintenanceRequest request)
    {
        if (!Enum.IsDefined(request.Priority))
            throw new ValidationException("Choose a valid maintenance priority.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 160)
            throw new ValidationException("Title is required and must be at most 160 characters.");
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 3000)
            throw new ValidationException("Description is required and must be at most 3000 characters.");
    }

    private async Task<string> CreateUniquePublicProfileCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = TokenUtilities.CreatePublicProfileCode();
            if (!await dbContext.Users.AnyAsync(x => x.PublicProfileCode == code, cancellationToken))
                return code;
        }

        throw new InvalidOperationException("Unable to generate a unique profile code.");
    }

    private static string NormalizeIndianPhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException("Phone number is required.");

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
            digits = digits[2..];

        if (digits.Length != 10 || digits[0] is < '6' or > '9')
            throw new ValidationException("Enter a valid 10-digit Indian mobile number.");

        return $"+91{digits}";
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
