using RentMaster.Domain.Common;
using RentMaster.Domain.Enums;

namespace RentMaster.Domain.Entities;

public sealed class IdentityDocument : BaseEntity
{
    public required string UserId { get; set; }
    public IdentityDocumentType DocumentType { get; set; }
    public required string DocumentNumber { get; set; }
    public required string NumberLast4 { get; set; }
    public required string NumberHash { get; set; }
    public required string StorageObjectName { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string ConsentVersion { get; set; }
    public DateTimeOffset ConsentAcceptedAtUtc { get; set; }
    public required string ConsentIpAddress { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public string? RejectionReason { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
}
