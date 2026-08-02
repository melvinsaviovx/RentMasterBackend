using RentMaster.Domain.Enums;

namespace RentMaster.Infrastructure.Configuration;

public sealed class VerificationOptions
{
    public const string SectionName = "Verification";

    public required string NumberHashPepper { get; init; }
    public long MaximumFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    // These are the accepted document types shown to the user.
    // The configured MinimumVerifiedDocuments value decides how many of them
    // must be approved before the account is considered verified.
    public IdentityDocumentType[] RequiredDocuments { get; init; } = [];

    // Phase 1 policy: approve any one accepted identity document
    // (Aadhaar OR Passport).
    public int MinimumVerifiedDocuments { get; init; } = 1;
}
