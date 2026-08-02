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

    [HttpPost("{id:guid}/cancel-pending")]
    public Task<TenancyDto> CancelPending(Guid id, CancellationToken cancellationToken) =>
        tenancyService.CancelPendingAsync(id, cancellationToken);

    [HttpPost("{id:guid}/request-end")]
    public Task<TenancyDto> RequestEnd(
        Guid id,
        RequestTenancyEndRequest request,
        CancellationToken cancellationToken) =>
        tenancyService.RequestEndAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/cancel-end-request")]
    public Task<TenancyDto> CancelEndRequest(Guid id, CancellationToken cancellationToken) =>
        tenancyService.CancelEndRequestAsync(id, cancellationToken);

    [HttpPost("{id:guid}/confirm-end")]
    public Task<TenancyDto> ConfirmEnd(Guid id, CancellationToken cancellationToken) =>
        tenancyService.ConfirmEndAsync(id, cancellationToken);

    [HttpPost("{id:guid}/complete-end")]
    public Task<TenancyDto> CompleteEnd(Guid id, CancellationToken cancellationToken) =>
        tenancyService.CompleteEndAsync(id, cancellationToken);
}
