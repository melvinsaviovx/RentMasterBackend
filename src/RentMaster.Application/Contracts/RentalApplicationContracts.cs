using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record CreateRentalApplicationRequest(
    DateOnly ExpectedMoveInDate,
    DateOnly? ExpectedMoveOutDate,
    int OccupantCount,
    string Message);

public sealed record DecideRentalApplicationRequest(string? Reason);

public sealed record RentalApplicationDto(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    string TenantProfileCode,
    string TenantDisplayName,
    DateOnly ExpectedMoveInDate,
    DateOnly? ExpectedMoveOutDate,
    int OccupantCount,
    string Message,
    RentalApplicationStatus Status,
    Guid? TenancyId,
    DateTimeOffset CreatedAtUtc,
    string RowVersion);
