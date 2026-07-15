using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IRentalApplicationService
{
    Task<RentalApplicationDto> ApplyAsync(Guid propertyId, CreateRentalApplicationRequest request, CancellationToken cancellationToken);
    Task<PagedResult<RentalApplicationDto>> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<RentalApplicationDto>> GetForPropertyAsync(Guid propertyId, int page, int pageSize, CancellationToken cancellationToken);
    Task<RentalApplicationDto> ShortlistAsync(Guid applicationId, DecideRentalApplicationRequest request, CancellationToken cancellationToken);
    Task<RentalApplicationDto> AcceptAsync(Guid applicationId, DecideRentalApplicationRequest request, CancellationToken cancellationToken);
    Task<RentalApplicationDto> RejectAsync(Guid applicationId, DecideRentalApplicationRequest request, CancellationToken cancellationToken);
    Task<RentalApplicationDto> WithdrawAsync(Guid applicationId, CancellationToken cancellationToken);
}
