using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface ITenancyService
{
    Task<TenancyDto> CreateAsync(CreateTenancyRequest request, CancellationToken cancellationToken);
    Task<TenancyDto> ConfirmAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<TenancyDto> RequestEndAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<TenancyDto> ConfirmEndAsync(Guid tenancyId, CancellationToken cancellationToken);
    Task<PagedResult<TenancyDto>> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
}
