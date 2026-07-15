using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Infrastructure.Configuration;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Persistence;
using RentMaster.Infrastructure.Security;

namespace RentMaster.Infrastructure.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext,
    JwtTokenService jwtTokenService,
    ICurrentUserService currentUser,
    IOptions<JwtOptions> jwtOptions)
    : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Role))
            throw new ValidationException("Role is required.");

        var role = AppRoles.SelfRegisterable
            .FirstOrDefault(x => x.Equals(request.Role.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("Role must be Owner or Tenant.");

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150)
            throw new ValidationException("Full name is required and must be at most 150 characters.");
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationException("Email is required.");
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            throw new ValidationException("Phone number is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Password is required.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account already exists for this email.");

        var publicProfileCode = await CreateUniquePublicProfileCodeAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            FullName = request.FullName.Trim(),
            PublicProfileCode = publicProfileCode
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(x => x.Description)));

        result = await userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(x => x.Description)));

        if (role == AppRoles.Owner)
            dbContext.OwnerProfiles.Add(new OwnerProfile { UserId = user.Id });
        else
            dbContext.TenantProfiles.Add(new TenantProfile { UserId = user.Id });

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await CreateSessionAsync(user, ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new UnauthorizedException("Invalid email or password.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Invalid email or password.");

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedException("Account is temporarily locked. Try again later.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException("Invalid email or password.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CreateSessionAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshRequest request,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new ValidationException("Refresh token is required.");

        var now = DateTimeOffset.UtcNow;
        var tokenHash = TokenUtilities.HashRefreshToken(request.RefreshToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (!existingToken.IsActive(now))
            throw new UnauthorizedException("Refresh token is expired or revoked.");

        var user = await userManager.FindByIdAsync(existingToken.UserId)
            ?? throw new UnauthorizedException("User no longer exists.");

        if (!user.IsActive)
            throw new UnauthorizedException("User account is inactive.");

        var rawNewRefreshToken = TokenUtilities.CreateRefreshToken();
        var newHash = TokenUtilities.HashRefreshToken(rawNewRefreshToken);
        var refreshExpires = now.AddDays(_jwtOptions.RefreshTokenDays);

        existingToken.RevokedAtUtc = now;
        existingToken.RevokedByIp = ipAddress;
        existingToken.ReplacedByTokenHash = newHash;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = ipAddress
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        var access = await jwtTokenService.CreateAccessTokenAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        await transaction.CommitAsync(cancellationToken);

        return new AuthResponse(
            access.Token,
            access.ExpiresAtUtc,
            rawNewRefreshToken,
            refreshExpires,
            user.Id,
            user.PublicProfileCode,
            user.Email ?? string.Empty,
            roles.ToArray());
    }

    public async Task LogoutAsync(
        LogoutRequest request,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var tokenHash = TokenUtilities.HashRefreshToken(request.RefreshToken);
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (token is null)
            return;

        if (token.UserId != currentUser.UserId)
            throw new ForbiddenException("The refresh token does not belong to the authenticated user.");

        if (token.RevokedAtUtc is null)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            token.RevokedByIp = ipAddress;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> CreateUniquePublicProfileCodeAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = TokenUtilities.CreatePublicProfileCode();
            if (!await dbContext.Users.AnyAsync(
                    x => x.PublicProfileCode == code,
                    cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique public profile code.");
    }

    private async Task<AuthResponse> CreateSessionAsync(
        ApplicationUser user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var access = await jwtTokenService.CreateAccessTokenAsync(user);
        var rawRefreshToken = TokenUtilities.CreateRefreshToken();
        var refreshExpires = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenUtilities.HashRefreshToken(rawRefreshToken),
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = ipAddress
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        var roles = await userManager.GetRolesAsync(user);

        return new AuthResponse(
            access.Token,
            access.ExpiresAtUtc,
            rawRefreshToken,
            refreshExpires,
            user.Id,
            user.PublicProfileCode,
            user.Email ?? string.Empty,
            roles.ToArray());
    }
}
