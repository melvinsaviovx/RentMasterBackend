using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/maintenance")]
public sealed class MaintenanceController(IMaintenanceService maintenanceService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.Owner + "," + AppRoles.Tenant)]
    public Task<MaintenanceRequestDto> Create(
        CreateMaintenanceRequest request,
        CancellationToken cancellationToken) =>
        maintenanceService.CreateAsync(request, cancellationToken);

    [HttpGet]
    [Authorize(Roles = AppRoles.Owner + "," + AppRoles.Tenant + "," + AppRoles.Maintenance + "," + AppRoles.Admin)]
    public Task<PagedResult<MaintenanceRequestDto>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        maintenanceService.GetMineAsync(page, pageSize, cancellationToken);

    [HttpGet("staff")]
    [Authorize(Roles = AppRoles.Owner + "," + AppRoles.Admin)]
    public Task<IReadOnlyList<MaintenanceStaffDto>> Staff(CancellationToken cancellationToken) =>
        maintenanceService.GetStaffAsync(cancellationToken);

    [HttpPost("staff")]
    [Authorize(Roles = AppRoles.Admin)]
    public Task<MaintenanceStaffDto> CreateStaff(
        CreateMaintenanceStaffRequest request,
        CancellationToken cancellationToken) =>
        maintenanceService.CreateStaffAsync(request, cancellationToken);

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = AppRoles.Owner + "," + AppRoles.Admin)]
    public Task<MaintenanceRequestDto> Assign(
        Guid id,
        AssignMaintenanceRequest request,
        CancellationToken cancellationToken) =>
        maintenanceService.AssignAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/status")]
    [Authorize(Roles = AppRoles.Owner + "," + AppRoles.Maintenance + "," + AppRoles.Admin)]
    public Task<MaintenanceRequestDto> UpdateStatus(
        Guid id,
        UpdateMaintenanceStatusRequest request,
        CancellationToken cancellationToken) =>
        maintenanceService.UpdateStatusAsync(id, request, cancellationToken);
}
