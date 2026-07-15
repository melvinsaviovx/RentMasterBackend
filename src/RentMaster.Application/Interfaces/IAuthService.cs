using RentMaster.Application.Contracts;

namespace RentMaster.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, string ipAddress, CancellationToken cancellationToken);
    Task LogoutAsync(LogoutRequest request, string ipAddress, CancellationToken cancellationToken);
}
