using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Configuration;
using RentMaster.Infrastructure.Persistence;
using RentMaster.Infrastructure.Security;

namespace RentMaster.Infrastructure.Services;

public sealed class IdentityVerificationService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IDocumentStorage documentStorage,
    IOptions<VerificationOptions> options,
    IdentityNumberProtector numberProtector,
    ILogger<IdentityVerificationService> logger)
    : IIdentityVerificationService
{
    private readonly VerificationOptions _options = options.Value;
    private readonly IdentityDocumentType[] _requiredDocuments =
        options.Value.RequiredDocuments.Distinct().ToArray();
    private readonly int _minimumVerifiedDocuments = options.Value.MinimumVerifiedDocuments;

    public async Task<IdentityDocumentDto> SubmitAsync(
        SubmitIdentityDocumentCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.DocumentType) || !_requiredDocuments.Contains(command.DocumentType))
            throw new ValidationException("This identity document type is not accepted.");
        if (string.IsNullOrWhiteSpace(command.DocumentNumber))
            throw new ValidationException("Identity document number is required.");

        await ValidateFileAsync(command, cancellationToken);

        if (!command.ConsentAccepted)
            throw new ValidationException("Identity verification consent is required.");
        if (string.IsNullOrWhiteSpace(command.ConsentVersion) || command.ConsentVersion.Trim().Length > 50)
            throw new ValidationException("A valid consent-policy version is required.");

        var normalizedNumber = NormalizeDocumentNumber(command.DocumentType, command.DocumentNumber);
        var numberHash = ComputeNumberHash(command.DocumentType, normalizedNumber);
        var last4 = normalizedNumber[^4..];

        var duplicateOwner = await dbContext.IdentityDocuments
            .Where(x => x.DocumentType == command.DocumentType &&
                        x.NumberHash == numberHash &&
                        (x.Status == VerificationStatus.Pending || x.Status == VerificationStatus.Verified))
            .Select(x => x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicateOwner is not null && duplicateOwner != currentUser.UserId)
            throw new ConflictException("This identity document is already linked to another account or awaiting review.");

        var existing = await dbContext.IdentityDocuments.SingleOrDefaultAsync(
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
                    DocumentNumber = numberProtector.Protect(normalizedNumber),
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
                existing.DocumentNumber = numberProtector.Protect(normalizedNumber);
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
                try
                {
                    await documentStorage.DeleteIfExistsAsync(previousObjectName, cancellationToken);
                }
                catch (Exception cleanupException)
                {
                    logger.LogWarning(cleanupException,
                        "Identity document {DocumentId} was resubmitted, but the previous stored file could not be deleted.",
                        existing.Id);
                }
                return Map(existing);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(existing);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await DeleteUploadedFileBestEffortAsync(storageObjectName);
            throw new ConflictException("This identity document is already linked to another account or awaiting review.");
        }
        catch
        {
            await DeleteUploadedFileBestEffortAsync(storageObjectName);
            throw;
        }
    }

    public async Task<VerificationStatusDto> GetMyStatusAsync(CancellationToken cancellationToken)
    {
        var documents = await dbContext.IdentityDocuments
            .Where(x => x.UserId == currentUser.UserId)
            .OrderBy(x => x.DocumentType)
            .ToListAsync(cancellationToken);

        var verifiedDocumentCount = documents
            .Where(x => x.Status == VerificationStatus.Verified && _requiredDocuments.Contains(x.DocumentType))
            .Select(x => x.DocumentType)
            .Distinct()
            .Count();

        return new VerificationStatusDto(
            verifiedDocumentCount >= _minimumVerifiedDocuments,
            documents.Select(Map).ToArray(),
            _requiredDocuments,
            _minimumVerifiedDocuments);
    }

    public async Task<bool> IsUserVerifiedAsync(string userId, CancellationToken cancellationToken)
    {
        var verifiedTypes = await dbContext.IdentityDocuments
            .Where(x => x.UserId == userId && x.Status == VerificationStatus.Verified)
            .Select(x => x.DocumentType)
            .ToListAsync(cancellationToken);

        return verifiedTypes.Where(_requiredDocuments.Contains).Distinct().Count() >= _minimumVerifiedDocuments;
    }

    public async Task<PagedResult<PendingIdentityDocumentDto>> GetPendingAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureReviewer();
        (page, pageSize) = NormalizePage(page, pageSize);
        var query =
            from document in dbContext.IdentityDocuments.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on document.UserId equals user.Id
            where document.Status == VerificationStatus.Pending
            orderby document.UpdatedAtUtc
            select new { Document = document, User = user };

        var count = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(x => new PendingIdentityDocumentDto(
                x.Document.Id,
                x.Document.UserId,
                x.User.FullName,
                x.User.Email ?? string.Empty,
                x.Document.DocumentType,
                ReadDocumentNumber(x.Document),
                x.Document.OriginalFileName,
                x.Document.ContentType,
                x.Document.SizeBytes,
                x.Document.UpdatedAtUtc))
            .ToArray();

        return new PagedResult<PendingIdentityDocumentDto>(items, page, pageSize, count);
    }

    public async Task<PagedResult<VerifiedIdentityUserDto>> GetVerifiedUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureReviewer();
        (page, pageSize) = NormalizePage(page, pageSize);

        // Load the verified records with their account details first, then group them
        // in memory. This keeps the query reliable across SQL Server versions and
        // correctly supports users who verified more than one accepted document.
        var verifiedRows = await (
            from document in dbContext.IdentityDocuments.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on document.UserId equals user.Id
            where document.Status == VerificationStatus.Verified &&
                  !document.IsDeleted &&
                  user.IsActive
            select new { Document = document, User = user })
            .ToListAsync(cancellationToken);

        var groupedUsers = verifiedRows
            .GroupBy(row => row.User.Id, StringComparer.Ordinal)
            .Select(group => new
            {
                User = group.First().User,
                VerifiedAtUtc = group.Max(row => row.Document.ReviewedAtUtc ?? row.Document.UpdatedAtUtc),
                Documents = group
                    .Select(row => row.Document)
                    .OrderBy(document => document.DocumentType)
                    .ToArray()
            })
            .OrderByDescending(row => row.VerifiedAtUtc)
            .ThenBy(row => row.User.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var total = groupedUsers.Length;
        var pageRows = groupedUsers
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var userIds = pageRows.Select(row => row.User.Id).ToArray();
        var roleRows = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId) && role.Name != null
            select new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleRows
            .GroupBy(row => row.UserId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.RoleName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        var items = pageRows.Select(row => new VerifiedIdentityUserDto(
            row.User.Id,
            row.User.FullName,
            row.User.Email ?? string.Empty,
            row.User.PhoneNumber ?? string.Empty,
            rolesByUser.GetValueOrDefault(row.User.Id, Array.Empty<string>()),
            row.User.PublicProfileCode,
            row.VerifiedAtUtc,
            row.Documents.Select(document => new VerifiedIdentityDocumentDto(
                document.Id,
                document.DocumentType,
                ReadDocumentNumber(document),
                document.OriginalFileName,
                document.ContentType,
                document.ReviewedAtUtc ?? document.UpdatedAtUtc))
                .ToArray()))
            .ToArray();

        return new PagedResult<VerifiedIdentityUserDto>(items, page, pageSize, total);
    }

    public async Task<StoredDocumentFile> OpenDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        EnsureReviewer();
        var document = await dbContext.IdentityDocuments.AsNoTracking()
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
        EnsureReviewer();
        var document = await dbContext.IdentityDocuments.SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("Identity document was not found.");
        if (document.Status != VerificationStatus.Pending)
            throw new ConflictException("Only pending documents can be reviewed.");
        if (!request.Approve && string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("A rejection reason is required.");
        if (request.Reason?.Trim().Length > 500)
            throw new ValidationException("Rejection reason must be at most 500 characters.");

        if (request.Approve)
        {
            var duplicateExists = await dbContext.IdentityDocuments.AsNoTracking().AnyAsync(
                x => x.Id != document.Id &&
                     x.DocumentType == document.DocumentType &&
                     x.NumberHash == document.NumberHash &&
                     (x.Status == VerificationStatus.Pending || x.Status == VerificationStatus.Verified),
                cancellationToken);
            if (duplicateExists)
                throw new ConflictException("This identity document is already linked to another account or awaiting review.");
        }

        document.Status = request.Approve ? VerificationStatus.Verified : VerificationStatus.Rejected;
        document.RejectionReason = request.Approve ? null : request.Reason!.Trim();
        document.ReviewedByUserId = currentUser.UserId;
        document.ReviewedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }


    private void EnsureReviewer()
    {
        if (!currentUser.IsInRole(AppRoles.Admin) && !currentUser.IsInRole(AppRoles.Moderator))
            throw new ForbiddenException("You do not have permission to review identity documents.");
    }

    private async Task ValidateFileAsync(SubmitIdentityDocumentCommand command, CancellationToken cancellationToken)
    {
        if (command.SizeBytes <= 0 || command.SizeBytes > _options.MaximumFileSizeBytes)
            throw new ValidationException("The selected file is too large.");

        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf", "image/jpeg", "image/png"
        };
        var allowedExtensions = new HashSet<string> { ".pdf", ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        if (!allowedContentTypes.Contains(command.ContentType) || !allowedExtensions.Contains(extension))
            throw new ValidationException("Choose a PDF, JPG or PNG document.");
        if (!command.Content.CanSeek)
            throw new ValidationException("The selected file cannot be read.");

        var header = new byte[8];
        var bytesRead = await command.Content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        command.Content.Position = 0;
        var isPdf = bytesRead >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;
        var isJpeg = bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = bytesRead >= 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var signatureMatches = command.ContentType.ToLowerInvariant() switch
        {
            "application/pdf" => isPdf,
            "image/jpeg" => isJpeg,
            "image/png" => isPng,
            _ => false
        };
        if (!signatureMatches)
            throw new ValidationException("The selected file does not match its file type.");
    }

    private string ComputeNumberHash(IdentityDocumentType type, string normalizedNumber)
    {
        if (string.IsNullOrWhiteSpace(_options.NumberHashPepper) || _options.NumberHashPepper.Length < 32)
            throw new InvalidOperationException("Verification settings are incomplete.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.NumberHashPepper));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{type}:{normalizedNumber}")));
    }

    private static string NormalizeDocumentNumber(IdentityDocumentType type, string rawValue)
    {
        var normalized = new string(rawValue.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (type == IdentityDocumentType.Aadhaar)
        {
            if (normalized.Length != 12 || !normalized.All(char.IsDigit))
                throw new ValidationException("Aadhaar number must contain exactly 12 digits.");
        }
        else
        {
            if (normalized.Length is < 6 or > 20)
                throw new ValidationException("Passport number must contain 6 to 20 letters or numbers.");
        }
        return normalized;
    }

    private IdentityDocumentDto Map(IdentityDocument document) => new(
        document.Id,
        document.DocumentType,
        ReadDocumentNumber(document),
        document.Status,
        document.RejectionReason,
        document.UpdatedAtUtc,
        document.ReviewedAtUtc);

    private async Task DeleteUploadedFileBestEffortAsync(string storageObjectName)
    {
        try { await documentStorage.DeleteIfExistsAsync(storageObjectName, CancellationToken.None); }
        catch (Exception cleanupException)
        {
            logger.LogWarning(cleanupException,
                "The uploaded identity document file {StorageObjectName} could not be deleted after the database operation failed.",
                storageObjectName);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private string ReadDocumentNumber(IdentityDocument document) =>
        numberProtector.TryUnprotect(document.DocumentNumber, out var number)
            ? number
            : $"Unavailable (ending {document.NumberLast4})";

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
