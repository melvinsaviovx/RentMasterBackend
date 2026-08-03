using RentMaster.Domain.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Domain.Entities;

public sealed class Property : BaseEntity
{
    public required string OwnerUserId { get; set; }
    public required string Title { get; set; }
    public required string AddressLine1 { get; set; }
    public required string Locality { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string PostalCode { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public PropertyStatus Status { get; set; } = PropertyStatus.Draft;

    public ICollection<PropertyPhoto> Photos { get; set; } = [];
    public ICollection<Tenancy> Tenancies { get; set; } = [];
    public ICollection<RentalApplication> RentalApplications { get; set; } = [];
}
