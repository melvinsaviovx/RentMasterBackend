using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/support")]
public sealed class SupportController(ISupportService supportService) : ControllerBase
{
    [HttpPost]
    public Task<SupportTicketDto> Create(
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken) =>
        supportService.CreateAsync(request, cancellationToken);

    [HttpGet("mine")]
    public Task<PagedResult<SupportTicketDto>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        supportService.GetMineAsync(page, pageSize, cancellationToken);
}
