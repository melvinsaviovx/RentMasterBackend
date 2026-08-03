using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/support")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Moderator}")]
public sealed class AdminSupportController(ISupportService supportService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SupportTicketDto>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        supportService.GetAllAsync(page, pageSize, cancellationToken);

    [HttpPost("{id:guid}/decision")]
    public Task<SupportTicketDto> Decide(
        Guid id,
        SupportTicketDecisionRequest request,
        CancellationToken cancellationToken) =>
        supportService.DecideAsync(id, request, cancellationToken);
}
