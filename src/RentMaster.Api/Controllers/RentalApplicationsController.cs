using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class RentalApplicationsController(IRentalApplicationService applicationService) : ControllerBase
{
    [HttpPost("properties/{propertyId:guid}/applications")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<RentalApplicationDto>> Apply(
        Guid propertyId,
        CreateRentalApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await applicationService.ApplyAsync(propertyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { }, result);
    }

    [HttpGet("applications/mine")]
    [Authorize(Roles = AppRoles.Tenant)]
    public Task<PagedResult<RentalApplicationDto>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        applicationService.GetMineAsync(page, pageSize, cancellationToken);

    [HttpGet("properties/{propertyId:guid}/applications")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<PagedResult<RentalApplicationDto>> GetForProperty(
        Guid propertyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        applicationService.GetForPropertyAsync(propertyId, page, pageSize, cancellationToken);

    [HttpPost("applications/{applicationId:guid}/shortlist")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<RentalApplicationDto> Shortlist(
        Guid applicationId,
        DecideRentalApplicationRequest request,
        CancellationToken cancellationToken) =>
        applicationService.ShortlistAsync(applicationId, request, cancellationToken);

    [HttpPost("applications/{applicationId:guid}/accept")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<RentalApplicationDto> Accept(
        Guid applicationId,
        DecideRentalApplicationRequest request,
        CancellationToken cancellationToken) =>
        applicationService.AcceptAsync(applicationId, request, cancellationToken);

    [HttpPost("applications/{applicationId:guid}/reject")]
    [Authorize(Roles = AppRoles.Owner)]
    public Task<RentalApplicationDto> Reject(
        Guid applicationId,
        DecideRentalApplicationRequest request,
        CancellationToken cancellationToken) =>
        applicationService.RejectAsync(applicationId, request, cancellationToken);

    [HttpPost("applications/{applicationId:guid}/withdraw")]
    [Authorize(Roles = AppRoles.Tenant)]
    public Task<RentalApplicationDto> Withdraw(
        Guid applicationId,
        CancellationToken cancellationToken) =>
        applicationService.WithdrawAsync(applicationId, cancellationToken);
}
