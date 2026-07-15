using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record TenancyDto(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    string OwnerUserId,
    string TenantUserId,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    DateOnly? ActualEndDate,
    decimal AgreedMonthlyRent,
    decimal AgreedSecurityDeposit,
    TenancyStatus Status,
    DateTimeOffset CreatedAtUtc);
