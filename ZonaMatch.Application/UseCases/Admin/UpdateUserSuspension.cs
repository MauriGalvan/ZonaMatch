using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// Suspends or reactivates an account. A suspended user cannot sign in (LoginUser); the session already open keeps working
// until its token expires. Administrators cannot be suspended, so nobody locks the admin panel by accident.
public sealed class UpdateUserSuspension(IUserRepository users, TimeProvider clock)
{
    public async Task<AdminUserResponse> ExecuteAsync(
        Guid userId, bool suspended, Guid administratorId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (suspended && userId == administratorId)
            throw new BadRequestException("No podés suspender tu propia cuenta.");

        if (suspended && user.IsAdministrator)
            throw new BadRequestException("No se puede suspender a un administrador. Quitale el rol antes de suspenderlo.");

        if (suspended != user.IsSuspended)
        {
            user.SuspendedAt = suspended ? clock.GetUtcNow() : null;
            await users.SetSuspendedAsync(userId, user.SuspendedAt, cancellationToken);
        }

        return UpdateInvestorAccess.ToResponse(user);
    }
}
