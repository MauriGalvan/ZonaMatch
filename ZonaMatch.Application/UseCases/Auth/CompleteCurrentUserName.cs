using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Auth;

public sealed class CompleteCurrentUserName(IUserRepository users)
{
    public async Task<CurrentSessionResponse> ExecuteAsync(
        AuthenticatedUserContext session,
        bool isAdmin,
        CompleteUserNameRequest request,
        CancellationToken cancellationToken = default)
    {
        var (firstName, lastName) = UserNameRules.Normalize(request.FirstName, request.LastName);

        var completed = await users.TryCompleteNameAsync(session.Id, firstName, lastName, cancellationToken);
        var user = await users.GetByIdAsync(session.Id, cancellationToken)
            ?? throw new UnauthorizedException("Iniciá sesión para continuar.");

        if (!completed)
            throw new ConflictException("Tu cuenta ya tiene nombre y apellido.");

        return new CurrentSessionResponse(session.Id, session.Email, isAdmin, user.FirstName, user.LastName);
    }
}
