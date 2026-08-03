using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace RentMaster.Infrastructure.Security;

internal static class TokenUtilities
{
    public static string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string CreatePublicProfileCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(9);
        return $"RM-{WebEncoders.Base64UrlEncode(bytes).ToUpperInvariant()}";
    }

    public static string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
