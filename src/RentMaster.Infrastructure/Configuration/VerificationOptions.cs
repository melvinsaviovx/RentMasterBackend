using RentMaster.Domain.Enums;

namespace RentMaster.Infrastructure.Configuration;

public sealed class VerificationOptions
{
    public const string SectionName = "Verification";

    public required string NumberHashPepper { get; init; }
    public long MaximumFileSizeBytes { get; init; } = 5 * 1024 * 1024;
    public IdentityDocumentType[] RequiredDocuments { get; init; } =
        [IdentityDocumentType.Aadhaar, IdentityDocumentType.Passport];
}
