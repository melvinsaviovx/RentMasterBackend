using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IReviewService
{
    Task<Guid> SubmitAsync(SubmitReviewRequest request, CancellationToken cancellationToken);
    Task<PagedResult<PublicReviewDto>> GetPublishedForUserAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
    Task DisputeAsync(Guid reviewId, DisputeReviewRequest request, CancellationToken cancellationToken);
    Task<PagedResult<PendingReviewDto>> GetPendingAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task DecideAsync(Guid reviewId, ReviewDecisionRequest request, CancellationToken cancellationToken);
}
