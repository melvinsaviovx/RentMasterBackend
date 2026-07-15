using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class ReviewScore : BaseEntity
{
    public Guid ReviewId { get; set; }
    public required string Category { get; set; }
    public int Score { get; set; }

    public Review Review { get; set; } = null!;
}
