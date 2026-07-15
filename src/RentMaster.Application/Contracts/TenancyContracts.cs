using RentMaster.Application.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record CreateTenancyRequest(
    Guid PropertyId,
    string TenantEmail,
    DateOnly StartDate,
    DateOnly? ExpectedEndDate,
    decimal AgreedMonthlyRent,
    decimal AgreedSecurityDeposit);

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
