using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class RentalApplicationService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IIdentityVerificationService verificationService,
    IChatService chatService,
    IndiaDateProvider dateProvider) : IRentalApplicationService
{
    public async Task<RentalApplicationDto> ApplyAsync(
        Guid propertyId,
        CreateRentalApplicationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        await EnsureVerifiedAsync(currentUser.UserId, cancellationToken);
        Validate(request);

        var property = await dbContext.Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken)
            ?? throw new NotFoundException("Property was not found.");

        if (property.Status != PropertyStatus.Published)
            throw new ConflictException("Applications are accepted only for published and available properties.");

        if (property.OwnerUserId == currentUser.UserId)
            throw new ForbiddenException("You cannot apply for your own property.");

        var hasDuplicate = await dbContext.RentalApplications.AnyAsync(
            x => x.PropertyId == propertyId &&
                 x.TenantUserId == currentUser.UserId &&
                 (x.Status == RentalApplicationStatus.Submitted ||
                  x.Status == RentalApplicationStatus.Shortlisted ||
                  x.Status == RentalApplicationStatus.Accepted),
            cancellationToken);

        if (hasDuplicate)
            throw new ConflictException("You already have an active application for this property.");

        var application = new RentalApplication
        {
            PropertyId = propertyId,
            TenantUserId = currentUser.UserId,
            ExpectedMoveInDate = request.ExpectedMoveInDate,
            ExpectedMoveOutDate = request.ExpectedMoveOutDate,
            OccupantCount = request.OccupantCount,
            Message = request.Message.Trim()
        };

        dbContext.RentalApplications.Add(application);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("You already have an active application for this property.");
        }

        await chatService.EnsureConversationAsync(
            property.Id,
            property.OwnerUserId,
            currentUser.UserId,
            application.Id,
            cancellationToken);
        await chatService.AddSystemMessageAsync(
            property.Id,
            property.OwnerUserId,
            currentUser.UserId,
            $"The tenant submitted a rental application for {application.ExpectedMoveInDate:dd MMM yyyy}.",
            cancellationToken);

        return await MapAsync(application.Id, cancellationToken);
    }

    public async Task<PagedResult<RentalApplicationDto>> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = dbContext.RentalApplications.AsNoTracking()
            .Where(x => x.TenantUserId == currentUser.UserId)
            .OrderByDescending(x => x.CreatedAtUtc);

        return await PageAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<RentalApplicationDto>> GetForPropertyAsync(
        Guid propertyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        (page, pageSize) = NormalizePage(page, pageSize);

        var ownsProperty = await dbContext.Properties.AnyAsync(
            x => x.Id == propertyId && x.OwnerUserId == currentUser.UserId,
            cancellationToken);

        if (!ownsProperty)
            throw new NotFoundException("Property was not found.");

        var query = dbContext.RentalApplications.AsNoTracking()
            .Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.CreatedAtUtc);

        return await PageAsync(query, page, pageSize, cancellationToken);
    }

    public Task<RentalApplicationDto> ShortlistAsync(
        Guid applicationId,
        DecideRentalApplicationRequest request,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(applicationId, RentalApplicationStatus.Shortlisted, request.Reason, cancellationToken);

    public Task<RentalApplicationDto> RejectAsync(
        Guid applicationId,
        DecideRentalApplicationRequest request,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(applicationId, RentalApplicationStatus.Rejected, request.Reason, cancellationToken);

    public async Task<RentalApplicationDto> AcceptAsync(
        Guid applicationId,
        DecideRentalApplicationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        await EnsureVerifiedAsync(currentUser.UserId, cancellationToken);
        var application = await dbContext.RentalApplications
            .Include(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new NotFoundException("Rental application was not found.");

        EnsurePropertyOwner(application);

        if (application.Status is not (RentalApplicationStatus.Submitted or RentalApplicationStatus.Shortlisted))
            throw new ConflictException("Only submitted or shortlisted applications can be accepted.");

        if (application.Property.Status != PropertyStatus.Published)
            throw new ConflictException("The property is no longer available.");

        if (application.ExpectedMoveInDate < dateProvider.Today)
            throw new ConflictException("The requested move-in date has passed. Ask the tenant to submit a new application.");

        await EnsureVerifiedAsync(application.TenantUserId, cancellationToken);

        var hasOpenTenancy = await dbContext.Tenancies.AnyAsync(
            x => x.PropertyId == application.PropertyId &&
                 x.Status != TenancyStatus.Ended &&
                 x.Status != TenancyStatus.Cancelled,
            cancellationToken);

        if (hasOpenTenancy)
            throw new ConflictException("The property already has an open tenancy.");

        var tenancy = new Tenancy
        {
            PropertyId = application.PropertyId,
            OwnerUserId = currentUser.UserId,
            TenantUserId = application.TenantUserId,
            StartDate = application.ExpectedMoveInDate,
            ExpectedEndDate = application.ExpectedMoveOutDate,
            AgreedMonthlyRent = application.Property.MonthlyRent,
            AgreedSecurityDeposit = application.Property.SecurityDeposit
        };

        var conversation = await dbContext.ChatConversations
            .SingleOrDefaultAsync(
                x => x.PropertyId == application.PropertyId &&
                     x.OwnerUserId == currentUser.UserId &&
                     x.TenantUserId == application.TenantUserId,
                cancellationToken);

        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                PropertyId = application.PropertyId,
                OwnerUserId = currentUser.UserId,
                TenantUserId = application.TenantUserId,
                RentalApplicationId = application.Id
            };
            dbContext.ChatConversations.Add(conversation);
        }

        var now = DateTimeOffset.UtcNow;

        dbContext.Tenancies.Add(tenancy);
        application.Property.Status = PropertyStatus.Reserved;
        application.Status = RentalApplicationStatus.Accepted;
        application.DecisionAtUtc = now;
        application.DecisionByUserId = currentUser.UserId;
        application.DecisionReason = NormalizeReason(request.Reason);
        application.TenancyId = tenancy.Id;
        application.Tenancy = tenancy;

        conversation.RentalApplicationId = application.Id;
        conversation.TenancyId = tenancy.Id;
        conversation.LastMessageAtUtc = now;

        dbContext.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderUserId = currentUser.UserId,
            Content = "The owner accepted the application. The tenant must confirm the tenancy invitation.",
            IsSystemMessage = true
        });

        try
        {
            // A single save keeps the application, property reservation, tenancy
            // invitation and conversation update consistent. SaveChanges already
            // uses a database transaction, so a separate user transaction is not
            // needed here.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This application changed while you were reviewing it. Refresh the page and try again.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("The property already has an accepted or active tenancy.");
        }

        return await MapAsync(application.Id, cancellationToken);
    }

    public async Task<RentalApplicationDto> WithdrawAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        var application = await dbContext.RentalApplications
            .SingleOrDefaultAsync(
                x => x.Id == applicationId && x.TenantUserId == currentUser.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Rental application was not found.");

        if (application.Status is not (RentalApplicationStatus.Submitted or RentalApplicationStatus.Shortlisted))
            throw new ConflictException("Only submitted or shortlisted applications can be withdrawn.");

        application.Status = RentalApplicationStatus.Withdrawn;
        application.DecisionAtUtc = DateTimeOffset.UtcNow;
        application.DecisionByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        var property = await dbContext.Properties.AsNoTracking()
            .SingleAsync(x => x.Id == application.PropertyId, cancellationToken);
        await chatService.AddSystemMessageAsync(
            application.PropertyId,
            property.OwnerUserId,
            application.TenantUserId,
            "The tenant withdrew the rental application.",
            cancellationToken);
        return await MapAsync(application.Id, cancellationToken);
    }

    private async Task<RentalApplicationDto> ChangeStatusAsync(
        Guid applicationId,
        RentalApplicationStatus status,
        string? reason,
        CancellationToken cancellationToken)
    {
        EnsureOwner();
        var application = await dbContext.RentalApplications
            .Include(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new NotFoundException("Rental application was not found.");

        EnsurePropertyOwner(application);

        if (application.Status is not (RentalApplicationStatus.Submitted or RentalApplicationStatus.Shortlisted))
            throw new ConflictException("This application can no longer be changed.");

        application.Status = status;
        application.DecisionAtUtc = DateTimeOffset.UtcNow;
        application.DecisionByUserId = currentUser.UserId;
        application.DecisionReason = NormalizeReason(reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        var actionText = status == RentalApplicationStatus.Shortlisted
            ? "The owner shortlisted the rental application."
            : "The owner rejected the rental application.";
        await chatService.AddSystemMessageAsync(
            application.PropertyId,
            application.Property.OwnerUserId,
            application.TenantUserId,
            actionText,
            cancellationToken);
        return await MapAsync(application.Id, cancellationToken);
    }

    private async Task<PagedResult<RentalApplicationDto>> PageAsync(
        IQueryable<RentalApplication> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var ids = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var items = new List<RentalApplicationDto>(ids.Count);
        foreach (var id in ids)
            items.Add(await MapAsync(id, cancellationToken));

        return new PagedResult<RentalApplicationDto>(items, page, pageSize, total);
    }

    private async Task<RentalApplicationDto> MapAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await (
            from application in dbContext.RentalApplications.AsNoTracking()
            join property in dbContext.Properties.AsNoTracking()
                on application.PropertyId equals property.Id
            join tenant in dbContext.Users.AsNoTracking()
                on application.TenantUserId equals tenant.Id
            where application.Id == id
            select new { application, property, tenant })
            .SingleAsync(cancellationToken);

        var conversationId = await dbContext.ChatConversations.AsNoTracking()
            .Where(x => x.PropertyId == result.application.PropertyId &&
                        x.TenantUserId == result.application.TenantUserId)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return new RentalApplicationDto(
            result.application.Id,
            result.application.PropertyId,
            result.property.Title,
            result.tenant.PublicProfileCode,
            result.tenant.FullName,
            result.application.ExpectedMoveInDate,
            result.application.ExpectedMoveOutDate,
            result.application.OccupantCount,
            result.application.Message,
            result.application.Status,
            result.application.TenancyId,
            conversationId,
            result.application.CreatedAtUtc,
            Convert.ToBase64String(result.application.RowVersion));
    }

    private void EnsurePropertyOwner(RentalApplication application)
    {
        if (application.Property.OwnerUserId != currentUser.UserId)
            throw new ForbiddenException("Only the property owner can manage this application.");
    }

    private void EnsureTenant()
    {
        if (!currentUser.IsInRole(AppRoles.Tenant))
            throw new ForbiddenException("Only tenants can apply for properties.");
    }

    private void EnsureOwner()
    {
        if (!currentUser.IsInRole(AppRoles.Owner))
            throw new ForbiddenException("Only owners can manage property applications.");
    }

    private async Task EnsureVerifiedAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await verificationService.IsUserVerifiedAsync(userId, cancellationToken))
            throw new ForbiddenException("Identity verification must be completed before applying or accepting an application.");
    }

    private void Validate(CreateRentalApplicationRequest request)
    {
        var today = dateProvider.Today;
        if (request.ExpectedMoveInDate < today)
            throw new ValidationException("Expected move-in date cannot be in the past.");
        if (request.ExpectedMoveOutDate.HasValue && request.ExpectedMoveOutDate <= request.ExpectedMoveInDate)
            throw new ValidationException("Expected move-out date must be after the move-in date.");
        if (request.OccupantCount is < 1 or > 30)
            throw new ValidationException("Occupant count must be between 1 and 30.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 1000)
            throw new ValidationException("Message is required and must be at most 1000 characters.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var value = reason.Trim();
        if (value.Length > 500)
            throw new ValidationException("Reason must be at most 500 characters.");
        return value;
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
