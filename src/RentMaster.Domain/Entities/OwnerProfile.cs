using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class OwnerProfile : BaseEntity
{
    public required string UserId { get; set; }
    public string? BusinessName { get; set; }
}
