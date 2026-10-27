using System.Security.Cryptography;
using System.Text;

namespace ZonaMatch.Application.UseCases.PasswordResets;

// The token travels in the emailed link; the database keeps only its hash.
public static class PasswordResetTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    // 32 random bytes in base64url: safe in a URL and impossible to guess
    public static string New() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
