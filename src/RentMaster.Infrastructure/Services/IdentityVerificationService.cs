using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Configuration;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class IdentityVerificationService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IDocumentStorage documentStorage,
    IOptions<VerificationOptions> options)
    : IIdentityVerificationService
{
    private readonly VerificationOptions _options = options.Value;

    public async Task<IdentityDocumentDto> SubmitAsync(
        SubmitIdentityDocumentCommand command,
        CancellationToken cancellationToken)
    {
        await ValidateFileAsync(command, cancellationToken);

        if (!command.ConsentAccepted)
            throw new ValidationException("Identity verification consent is required.");

        if (string.IsNullOrWhiteSpace(command.ConsentVersion) ||
            command.ConsentVersion.Trim().Length > 50)
        {
            throw new ValidationException("A valid consent-policy version is required.");
        }

        var normalizedNumber = NormalizeDocumentNumber(command.DocumentType, command.DocumentNumber);
        var numberHash = ComputeNumberHash(command.DocumentType, normalizedNumber);
        var last4 = normalizedNumber[^4..];

        var duplicateOwner = await dbContext.IdentityDocuments
            .Where(x => x.NumberHash == numberHash && x.Status == VerificationStatus.Verified)
            .Select(x => x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicateOwner is not null && duplicateOwner != currentUser.UserId)
            throw new ConflictException("This identity document is already linked to another verified account.");

        var existing = await dbContext.IdentityDocuments
            .SingleOrDefaultAsync(
                x => x.UserId == currentUser.UserId && x.DocumentType == command.DocumentType,
                cancellationToken);

        if (existing?.Status == VerificationStatus.Verified)
            throw new ConflictException("A verified document of this type already exists.");

        var storageObjectName = await documentStorage.SaveAsync(
            currentUser.UserId,
            command.FileName,
            command.ContentType,
            command.Content,
            cancellationToken);

        try
        {
            if (existing is null)
            {
                existing = new IdentityDocument
                {
                    UserId = currentUser.UserId,
                    DocumentType = command.DocumentType,
                    NumberLast4 = last4,
                    NumberHash = numberHash,
                    StorageObjectName = storageObjectName,
                    OriginalFileName = Path.GetFileName(command.FileName),
                    ContentType = command.ContentType,
                    SizeBytes = command.SizeBytes,
                    ConsentVersion = command.ConsentVersion.Trim(),
                    ConsentAcceptedAtUtc = DateTimeOffset.UtcNow,
                    ConsentIpAddress = command.IpAddress,
                    Status = VerificationStatus.Pending
                };
                dbContext.IdentityDocuments.Add(existing);
            }
            else
            {
                var previousObjectName = existing.StorageObjectName;
                existing.NumberLast4 = last4;
                existing.NumberHash = numberHash;
                existing.StorageObjectName = storageObjectName;
                existing.OriginalFileName = Path.GetFileName(command.FileName);
                existing.ContentType = command.ContentType;
                existing.SizeBytes = command.SizeBytes;
                existing.ConsentVersion = command.ConsentVersion.Trim();
                existing.ConsentAcceptedAtUtc = DateTimeOffset.UtcNow;
                existing.ConsentIpAddress = command.IpAddress;
                existing.Status = VerificationStatus.Pending;
                existing.RejectionReason = null;
                existing.ReviewedByUserId = null;
                existing.ReviewedAtUtc = null;

                await dbContext.SaveChangesAsync(cancellationToken);
                await documentStorage.DeleteIfExistsAsync(previousObjectName, cancellationToken);
                return Map(existing);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(existing);
        }
        catch
        {
            await documentStorage.DeleteIfExistsAsync(storageObjectName, cancellationToken);
            throw;
        }
    }

    public async Task<VerificationStatusDto> GetMyStatusAsync(CancellationToken cancellationToken)
    {
        var documents = await dbContext.IdentityDocuments
            .Where(x => x.UserId == currentUser.UserId)
            .OrderBy(x => x.DocumentType)
            .ToListAsync(cancellationToken);

        var complete = _options.RequiredDocuments.All(required =>
            documents.Any(x => x.DocumentType == required && x.Status == VerificationStatus.Verified));

        return new VerificationStatusDto(
            complete,
            documents.Select(Map).ToArray(),
            _options.RequiredDocuments);
    }

    public async Task<bool> IsUserVerifiedAsync(string userId, CancellationToken cancellationToken)
    {
        var verifiedTypes = await dbContext.IdentityDocuments
            .Where(x => x.UserId == userId && x.Status == VerificationStatus.Verified)
            .Select(x => x.DocumentType)
            .ToListAsync(cancellationToken);

        return _options.RequiredDocuments.All(verifiedTypes.Contains);
    }

    public async Task<PagedResult<PendingIdentityDocumentDto>> GetPendingAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = dbContext.IdentityDocuments
            .AsNoTracking()
            .Where(x => x.Status == VerificationStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc);

        var count = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PendingIdentityDocumentDto(
                x.Id,
                x.UserId,
                x.DocumentType,
                $"****{x.NumberLast4}",
                x.OriginalFileName,
                x.ContentType,
                x.SizeBytes,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<PendingIdentityDocumentDto>(items, page, pageSize, count);
    }

    public async Task<StoredDocumentFile> OpenDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.IdentityDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("Identity document was not found.");

        var stream = await documentStorage.OpenReadAsync(document.StorageObjectName, cancellationToken);
        return new StoredDocumentFile(stream, document.ContentType, document.OriginalFileName);
    }

    public async Task DecideAsync(
        Guid documentId,
        VerificationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.IdentityDocuments
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("Identity document was not found.");

        if (document.Status != VerificationStatus.Pending)
            throw new ConflictException("Only pending documents can be reviewed.");

        if (!request.Approve && string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("A rejection reason is required.");

        document.Status = request.Approve
            ? VerificationStatus.Verified
            : VerificationStatus.Rejected;
        document.RejectionReason = request.Approve ? null : request.Reason!.Trim();
        document.ReviewedByUserId = currentUser.UserId;
        document.ReviewedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateFileAsync(
        SubmitIdentityDocumentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SizeBytes <= 0 || command.SizeBytes > _options.MaximumFileSizeBytes)
            throw new ValidationException($"File size must be between 1 byte and {_options.MaximumFileSizeBytes} bytes.");

        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string> { ".pdf", ".jpg", ".jpeg", ".png" };

        if (!allowedContentTypes.Contains(command.ContentType) || !allowedExtensions.Contains(extension))
            throw new ValidationException("Only PDF, JPEG, and PNG identity documents are allowed.");

        if (!command.Content.CanSeek)
            throw new ValidationException("The uploaded file stream must support validation.");

        var header = new byte[8];
        var bytesRead = await command.Content.ReadAsync(
            header.AsMemory(0, header.Length),
            cancellationToken);
        command.Content.Position = 0;

        var isPdf = bytesRead >= 4 &&
                    header[0] == 0x25 && header[1] == 0x50 &&
                    header[2] == 0x44 && header[3] == 0x46;
        var isJpeg = bytesRead >= 3 &&
                     header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = bytesRead >= 8 &&
                    header.SequenceEqual(new byte[]
                    {
                        0x89, 0x50, 0x4E, 0x47,
                        0x0D, 0x0A, 0x1A, 0x0A
                    });

        var signatureMatches = command.ContentType.ToLowerInvariant() switch
        {
            "application/pdf" => isPdf,
            "image/jpeg" => isJpeg,
            "image/png" => isPng,
            _ => false
        };

        if (!signatureMatches)
            throw new ValidationException("The uploaded file content does not match its declared type.");
    }

    private string ComputeNumberHash(IdentityDocumentType type, string normalizedNumber)
    {
        if (string.IsNullOrWhiteSpace(_options.NumberHashPepper) ||
            _options.NumberHashPepper.Length < 32)
        {
            throw new InvalidOperationException("Verification:NumberHashPepper must contain at least 32 characters.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.NumberHashPepper));
        var value = $"{type}:{normalizedNumber}";
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string NormalizeDocumentNumber(IdentityDocumentType type, string rawValue)
    {
        var normalized = new string(rawValue
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (type == IdentityDocumentType.Aadhaar)
        {
            if (normalized.Length != 12 || !normalized.All(char.IsDigit))
                throw new ValidationException("Aadhaar number must contain exactly 12 digits.");
        }
        else if (normalized.Length is < 6 or > 20)
        {
            throw new ValidationException("Passport number length is invalid.");
        }

        return normalized;
    }

    private static IdentityDocumentDto Map(IdentityDocument document) =>
        new(
            document.Id,
            document.DocumentType,
            $"****{document.NumberLast4}",
            document.Status,
            document.RejectionReason,
            document.CreatedAtUtc);

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
