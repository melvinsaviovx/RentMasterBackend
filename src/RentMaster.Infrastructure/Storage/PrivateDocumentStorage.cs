using Microsoft.Extensions.Configuration;
using RentMaster.Application.Common;
using RentMaster.Application.Interfaces;

namespace RentMaster.Infrastructure.Storage;

public sealed class PrivateDocumentStorage : IDocumentStorage
{
    private readonly string _rootPath;

    public PrivateDocumentStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["DocumentStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException("DocumentStorage:RootPath is not configured.");

        _rootPath = Path.GetFullPath(configuredPath);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        string userId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var objectName = Path.Combine(
            SanitizeSegment(userId),
            $"{Guid.NewGuid():N}{extension}");

        var fullPath = ResolveSafePath(objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var output = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);

        await content.CopyToAsync(output, cancellationToken);
        return objectName.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string objectName, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafePath(objectName);
        if (!File.Exists(fullPath))
            throw new NotFoundException("Stored file was not found.");

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    public Task DeleteIfExistsAsync(string objectName, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafePath(objectName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private string ResolveSafePath(string objectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        var combined = Path.GetFullPath(Path.Combine(_rootPath, objectName));
        var root = Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Invalid storage object path.");

        return combined;
    }

    private static string SanitizeSegment(string value)
    {
        var segment = string.Concat(value.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(segment))
            throw new ForbiddenException("Invalid storage user identifier.");

        return segment;
    }
}
