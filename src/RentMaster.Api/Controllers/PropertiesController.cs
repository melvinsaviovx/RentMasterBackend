using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/properties")]
public sealed class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<OwnerPropertyDto>> Create(
        CreatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await propertyService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { }, result);
    }

    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<PagedResult<OwnerPropertyDto>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        propertyService.GetMineAsync(page, pageSize, cancellationToken);

    [HttpGet("search")]
    public Task<PagedResult<PublicPropertyDto>> Search(
        [FromQuery] PropertySearchRequest request,
        CancellationToken cancellationToken) =>
        propertyService.SearchAsync(request, cancellationToken);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<OwnerPropertyDto> Update(
        Guid id,
        UpdatePropertyRequest request,
        CancellationToken cancellationToken) =>
        propertyService.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Owner)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await propertyService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
