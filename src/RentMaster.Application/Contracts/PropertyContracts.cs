using RentMaster.Application.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record CreatePropertyRequest(
    string Title,
    string AddressLine1,
    string Locality,
    string City,
    string State,
    string PostalCode,
    decimal MonthlyRent,
    decimal SecurityDeposit,
    int Bedrooms,
    int Bathrooms,
    bool Publish);

public sealed record UpdatePropertyRequest(
    string Title,
    string AddressLine1,
    string Locality,
    string City,
    string State,
    string PostalCode,
    decimal MonthlyRent,
    decimal SecurityDeposit,
    int Bedrooms,
    int Bathrooms,
    PropertyStatus Status,
    string RowVersion);

public sealed record OwnerPropertyDto(
    Guid Id,
    string Title,
    string AddressLine1,
    string Locality,
    string City,
    string State,
    string PostalCode,
    decimal MonthlyRent,
    decimal SecurityDeposit,
    int Bedrooms,
    int Bathrooms,
    PropertyStatus Status,
    string RowVersion);

public sealed record PublicPropertyDto(
    Guid Id,
    string OwnerProfileCode,
    string Title,
    string Locality,
    string City,
    string State,
    string PostalCode,
    decimal MonthlyRent,
    decimal SecurityDeposit,
    int Bedrooms,
    int Bathrooms);

public sealed record PropertySearchRequest(
    string? City,
    string? Locality,
    decimal? MaximumRent,
    int? MinimumBedrooms,
    int Page = 1,
    int PageSize = 20);
