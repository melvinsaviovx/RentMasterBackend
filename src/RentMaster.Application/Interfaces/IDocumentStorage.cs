namespace RentMaster.Application.Interfaces;

public interface IDocumentStorage
{
    Task<string> SaveAsync(
        string userId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string objectName, CancellationToken cancellationToken);
    Task DeleteIfExistsAsync(string objectName, CancellationToken cancellationToken);
}
