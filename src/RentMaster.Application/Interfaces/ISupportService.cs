using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface ISupportService
{
    Task<SupportTicketDto> CreateAsync(CreateSupportTicketRequest request, CancellationToken cancellationToken);
    Task<PagedResult<SupportTicketDto>> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<SupportTicketDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<SupportTicketDto> DecideAsync(Guid id, SupportTicketDecisionRequest request, CancellationToken cancellationToken);
}
