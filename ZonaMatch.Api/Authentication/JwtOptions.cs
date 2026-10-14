using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ZonaMatch.Api.Authentication
{
    // Section "Jwt". SigningKey must NOT be committed: use user-secrets in development and the
    // environment variable Jwt__SigningKey in production.
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        // HS256 needs at least 256 bits
        [Required, MinLength(32)]
        public string SigningKey { get; set; } = string.Empty;

        [Required]
        public string Issuer { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Range(1, 1440)]
        public int ExpirationMinutes { get; set; } = 60;

        public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
    }
}
