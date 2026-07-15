using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/tenancies")]
public sealed class TenanciesController(ITenancyService tenancyService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.Owner)]
    public async Task<ActionResult<TenancyDto>> Create(
        CreateTenancyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tenancyService.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/tenancies/{result.Id}", result);
    }

    [HttpGet("mine")]
    public Task<PagedResult<TenancyDto>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        tenancyService.GetMineAsync(page, pageSize, cancellationToken);

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = AppRoles.Tenant)]
    public Task<TenancyDto> Confirm(Guid id, CancellationToken cancellationToken) =>
        tenancyService.ConfirmAsync(id, cancellationToken);

    [HttpPost("{id:guid}/request-end")]
    public Task<TenancyDto> RequestEnd(Guid id, CancellationToken cancellationToken) =>
        tenancyService.RequestEndAsync(id, cancellationToken);

    [HttpPost("{id:guid}/confirm-end")]
    public Task<TenancyDto> ConfirmEnd(Guid id, CancellationToken cancellationToken) =>
        tenancyService.ConfirmEndAsync(id, cancellationToken);
}
