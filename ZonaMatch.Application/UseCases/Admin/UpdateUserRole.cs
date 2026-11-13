using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// Grants or removes the administrator role. The change applies from the user's next sign-in,
// because the role travels in the access token.
public sealed class UpdateUserRole(IUserRepository users)
{
    public async Task<AdminUserResponse> ExecuteAsync(
        Guid userId, bool isAdmin, Guid administratorId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        // Prevents an administrator from locking themselves out of the admin panel.
        if (userId == administratorId && !isAdmin)
            throw new BadRequestException("No podés quitarte tu propio rol de administrador.");

        if (user.IsAdministrator != isAdmin)
            await users.SetAdministratorAsync(userId, isAdmin, cancellationToken);

        user.IsAdministrator = isAdmin;
        return UpdateInvestorAccess.ToResponse(user);
    }
}
