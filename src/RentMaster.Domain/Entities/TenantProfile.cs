using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class TenantProfile : BaseEntity
{
    public required string UserId { get; set; }
}
