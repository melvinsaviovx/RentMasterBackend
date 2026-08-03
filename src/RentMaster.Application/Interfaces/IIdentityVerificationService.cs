using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IIdentityVerificationService
{
    Task<IdentityDocumentDto> SubmitAsync(SubmitIdentityDocumentCommand command, CancellationToken cancellationToken);
    Task<VerificationStatusDto> GetMyStatusAsync(CancellationToken cancellationToken);
    Task<bool> IsUserVerifiedAsync(string userId, CancellationToken cancellationToken);
    Task<PagedResult<PendingIdentityDocumentDto>> GetPendingAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<VerifiedIdentityUserDto>> GetVerifiedUsersAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<StoredDocumentFile> OpenDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task DecideAsync(Guid documentId, VerificationDecisionRequest request, CancellationToken cancellationToken);
}
