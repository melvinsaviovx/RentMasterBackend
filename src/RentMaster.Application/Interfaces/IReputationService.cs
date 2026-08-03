using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IReputationService
{
    Task<ReputationProfileDto> GetByCodeAsync(
        string profileCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ReputationProfileDto> GetMineAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
