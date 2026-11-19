using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;

namespace ZonaMatch.Application.UseCases.Auth;

public sealed class GetCurrentSession(IUserRepository users)
{
    public async Task<CurrentSessionResponse> ExecuteAsync(
        AuthenticatedUserContext session, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(session.Id, cancellationToken)
            ?? throw new UnauthorizedException("Iniciá sesión para continuar.");

        return new CurrentSessionResponse(session.Id, session.Email, isAdmin, user.FirstName, user.LastName);
    }
}
