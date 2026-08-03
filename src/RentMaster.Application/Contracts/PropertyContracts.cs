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

public sealed record PropertyPhotoDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    int SortOrder);

public sealed record UploadPropertyPhotoCommand(
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record StoredPropertyPhoto(
    Stream Content,
    string ContentType,
    string DownloadFileName);

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
    Guid? CoverPhotoId,
    int PhotoCount,
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
    int Bathrooms,
    Guid? CoverPhotoId);

public sealed record PropertySearchRequest(
    string? City,
    string? Locality,
    decimal? MaximumRent,
    int? MinimumBedrooms,
    int Page = 1,
    int PageSize = 20);

public sealed record PublicPropertyDetailsDto(
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
    int Bathrooms,
    PropertyStatus Status,
    bool HasActiveApplication,
    IReadOnlyList<PropertyPhotoDto> Photos);
