using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Api.Authentication
{
    // Lee quien hace el pedido a partir del JWT ya validado (los mismos claims que /Auth/me)
    public static class ClaimsPrincipalExtensions
    {
        // Null en pedidos anonimos o con un token sin los claims esperados
        public static SesionDto? Sesion(this ClaimsPrincipal usuario)
        {
            var id = usuario.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var email = usuario.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

            return Guid.TryParse(id, out var usuarioId) && email is not null ? new SesionDto(usuarioId, email) : null;
        }

        public static Guid? UsuarioId(this ClaimsPrincipal usuario) => usuario.Sesion()?.Id;
    }
}
