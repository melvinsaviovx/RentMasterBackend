using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RentMaster.Application.Common;
using RentMaster.Application.Interfaces;

namespace RentMaster.Infrastructure.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedException("Authenticated user identifier is missing.");

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
