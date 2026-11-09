using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Application.Exceptions;
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

    // For endpoints that require a session: without a valid user the global handler answers 401 with ProblemDetails.
    public static AuthenticatedUserContext RequireUserContext(this ClaimsPrincipal principal) =>
        principal.GetAuthenticatedUserContext() ?? throw new UnauthorizedException(SessionRequiredMessage);

    public static Guid RequireUserId(this ClaimsPrincipal principal) => principal.RequireUserContext().Id;

    private const string SessionRequiredMessage = "Iniciá sesión para continuar.";
}
