using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record CreateMaintenanceRequest(
    Guid TenancyId,
    string Title,
    string Description,
    MaintenancePriority Priority);

public sealed record AssignMaintenanceRequest(string MaintenanceUserId);

public sealed record UpdateMaintenanceStatusRequest(
    MaintenanceRequestStatus Status,
    string? Note);

public sealed record CreateMaintenanceStaffRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string TemporaryPassword);

public sealed record MaintenanceStaffDto(
    string UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    bool IsActive);

public sealed record MaintenanceRequestDto(
    Guid Id,
    Guid TenancyId,
    Guid PropertyId,
    string PropertyTitle,
    string CreatedByUserId,
    string CreatedByDisplayName,
    string? AssignedToUserId,
    string? AssignedToDisplayName,
    string Title,
    string Description,
    MaintenancePriority Priority,
    MaintenanceRequestStatus Status,
    string? MaintenanceNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AssignedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? ClosedAtUtc);
