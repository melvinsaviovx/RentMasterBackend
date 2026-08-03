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
    string DocumentNumber,
    VerificationStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc);

public sealed record VerificationStatusDto(
    bool IsComplete,
    IReadOnlyList<IdentityDocumentDto> Documents,
    IReadOnlyList<IdentityDocumentType> RequiredDocuments,
    int MinimumVerifiedDocuments);

public sealed record VerificationDecisionRequest(
    bool Approve,
    string? Reason);

public sealed record PendingIdentityDocumentDto(
    Guid Id,
    string UserId,
    string UserFullName,
    string UserEmail,
    IdentityDocumentType DocumentType,
    string DocumentNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset SubmittedAtUtc);

public sealed record VerifiedIdentityDocumentDto(
    Guid Id,
    IdentityDocumentType DocumentType,
    string DocumentNumber,
    string OriginalFileName,
    string ContentType,
    DateTimeOffset VerifiedAtUtc);

public sealed record VerifiedIdentityUserDto(
    string UserId,
    string UserFullName,
    string UserEmail,
    string UserPhoneNumber,
    IReadOnlyList<string> Roles,
    string PublicProfileCode,
    DateTimeOffset VerifiedAtUtc,
    IReadOnlyList<VerifiedIdentityDocumentDto> Documents);

public sealed record StoredDocumentFile(
    Stream Content,
    string ContentType,
    string DownloadFileName);
