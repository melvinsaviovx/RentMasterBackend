using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/reputation")]
public sealed class ReputationController(IReputationService reputationService)
    : ControllerBase
{
    [HttpGet("me")]
    public Task<ReputationProfileDto> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        reputationService.GetMineAsync(page, pageSize, cancellationToken);

    [HttpGet("{profileCode}")]
    public Task<ReputationProfileDto> Get(
        string profileCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        reputationService.GetByCodeAsync(
            profileCode,
            page,
            pageSize,
            cancellationToken);
}
