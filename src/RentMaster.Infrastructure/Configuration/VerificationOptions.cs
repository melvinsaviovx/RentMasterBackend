using RentMaster.Domain.Enums;

namespace RentMaster.Infrastructure.Configuration;

public sealed class VerificationOptions
{
    public const string SectionName = "Verification";

    public required string NumberHashPepper { get; init; }
    public long MaximumFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    // Keep this empty in code. The configured values in appsettings are the
    // single source of truth; initialising the array with values here can cause
    // configuration binding to append the same documents a second time.
    public IdentityDocumentType[] RequiredDocuments { get; init; } = [];
}
