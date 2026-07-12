using System.Security.Cryptography;
using System.Text;

namespace InteriorDesignWeb.Services;

public static class RefreshTokenService
{
    public static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();
    }
}
