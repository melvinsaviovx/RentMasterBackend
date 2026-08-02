using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface ITenancyService
{
    Task<TenancyDto> ConfirmAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<TenancyDto> CancelPendingAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<TenancyDto> RequestEndAsync(Guid tenancyId, RequestTenancyEndRequest request, CancellationToken cancellationToken);
    Task<TenancyDto> CancelEndRequestAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<TenancyDto> ConfirmEndAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<TenancyDto> CompleteEndAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<PagedResult<TenancyDto>> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
}
