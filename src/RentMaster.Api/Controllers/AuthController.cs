using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;

namespace RentMaster.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("public-auth")]
    public Task<AuthResponse> Register(
        RegisterRequest request,
        CancellationToken cancellationToken) =>
        authService.RegisterAsync(request, GetIpAddress(), cancellationToken);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("public-auth")]
    public Task<AuthResponse> Login(
        LoginRequest request,
        CancellationToken cancellationToken) =>
        authService.LoginAsync(request, GetIpAddress(), cancellationToken);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("public-auth")]
    public Task<AuthResponse> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken) =>
        authService.RefreshAsync(request, GetIpAddress(), cancellationToken);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, GetIpAddress(), cancellationToken);
        return NoContent();
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
