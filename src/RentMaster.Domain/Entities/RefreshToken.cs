using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class RefreshToken : BaseEntity
{
    public required string UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public required string CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAtUtc is null && ExpiresAtUtc > now;
}
