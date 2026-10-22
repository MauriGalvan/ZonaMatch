using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Application.Models;

namespace ZonaMatch.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static AuthenticatedUserContext? GetAuthenticatedUserContext(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) && email is not null
            ? new AuthenticatedUserContext(userId, email)
            : null;
    }

    public static Guid? GetAuthenticatedUserId(this ClaimsPrincipal principal) =>
        principal.GetAuthenticatedUserContext()?.Id;
}
