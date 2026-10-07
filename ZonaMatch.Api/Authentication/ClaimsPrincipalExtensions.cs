using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Api.Authentication
{
    // Reads the caller from the validated JWT (same claims as /Auth/me)
    public static class ClaimsPrincipalExtensions
    {
        // Null for anonymous requests or a token without the expected claims
        public static SesionDto? Sesion(this ClaimsPrincipal user)
        {
            var id = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var email = user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

            return Guid.TryParse(id, out var usuarioId) && email is not null ? new SesionDto(usuarioId, email) : null;
        }

        public static Guid? UsuarioId(this ClaimsPrincipal user) => user.Sesion()?.Id;
    }
}
