using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.Authentication
{
    // Issues the access tokens that ConfigureJwtBearerOptions validates on every request
    public sealed class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly SigningCredentials _credentials;
        private readonly JsonWebTokenHandler _handler = new();

        public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
            _credentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
        }

        public (string Token, DateTimeOffset ExpiraEn) Generar(Usuario usuario)
        {
            var ahora = _timeProvider.GetUtcNow();
            var expiraEn = ahora.AddMinutes(_options.ExpirationMinutes);

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                    new(JwtRegisteredClaimNames.Email, usuario.Email),
                    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = ahora.UtcDateTime,
                NotBefore = ahora.UtcDateTime,
                Expires = expiraEn.UtcDateTime,
                SigningCredentials = _credentials
            };

            return (_handler.CreateToken(descriptor), expiraEn);
        }
    }
}
