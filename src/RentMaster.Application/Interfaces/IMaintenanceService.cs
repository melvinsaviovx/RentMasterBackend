using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IMaintenanceService
{
    Task<MaintenanceRequestDto> CreateAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken);
    Task<PagedResult<MaintenanceRequestDto>> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<MaintenanceStaffDto>> GetStaffAsync(CancellationToken cancellationToken);
    Task<MaintenanceStaffDto> CreateStaffAsync(CreateMaintenanceStaffRequest request, CancellationToken cancellationToken);
    Task<MaintenanceRequestDto> AssignAsync(Guid id, AssignMaintenanceRequest request, CancellationToken cancellationToken);
    Task<MaintenanceRequestDto> UpdateStatusAsync(Guid id, UpdateMaintenanceStatusRequest request, CancellationToken cancellationToken);
}
