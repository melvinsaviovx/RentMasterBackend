using RentMaster.Application.Common;
using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IPropertyService
{
    Task<OwnerPropertyDto> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken);
    Task<OwnerPropertyDto> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<OwnerPropertyDto>> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<PublicPropertyDto>> SearchAsync(PropertySearchRequest request, CancellationToken cancellationToken);
    Task<PublicPropertyDetailsDto> GetPublicDetailsAsync(Guid id, CancellationToken cancellationToken);
}
