using RentMaster.Domain.Enums;

namespace RentMaster.Application.Contracts;

public sealed record SubmitIdentityDocumentCommand(
    IdentityDocumentType DocumentType,
    string DocumentNumber,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    bool ConsentAccepted,
    string ConsentVersion,
    string IpAddress);

public sealed record IdentityDocumentDto(
    Guid Id,
    IdentityDocumentType DocumentType,
    string MaskedNumber,
    VerificationStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAtUtc);

public sealed record VerificationStatusDto(
    bool IsComplete,
    IReadOnlyList<IdentityDocumentDto> Documents,
    IReadOnlyList<IdentityDocumentType> RequiredDocuments);

public sealed record VerificationDecisionRequest(
    bool Approve,
    string? Reason);

public sealed record PendingIdentityDocumentDto(
    Guid Id,
    string UserId,
    IdentityDocumentType DocumentType,
    string MaskedNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset SubmittedAtUtc);

public sealed record StoredDocumentFile(
    Stream Content,
    string ContentType,
    string DownloadFileName);
