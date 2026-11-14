using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Admin;

// Grants or removes the administrator role. The change applies from the user's next sign-in,
// because the role travels in the access token. Granting it notifies the user, like the investor profile does.
public sealed class UpdateUserRole(IUserRepository users, INotificationRepository notifications, TimeProvider clock)
{
    public const string AdminDashboardLink = "/admin";

    public async Task<AdminUserResponse> ExecuteAsync(
        Guid userId, bool isAdmin, Guid administratorId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        // Prevents an administrator from locking themselves out of the admin panel.
        if (userId == administratorId && !isAdmin)
            throw new BadRequestException("No podés quitarte tu propio rol de administrador.");

        if (user.IsAdministrator != isAdmin)
        {
            await users.SetAdministratorAsync(userId, isAdmin, cancellationToken);

            if (isAdmin)
            {
                notifications.AddRange([new UserNotification
                {
                    UserId = userId,
                    Type = NotificationType.AdministratorRoleGranted,
                    Title = "Rol de administrador habilitado",
                    Message = "Ya podés entrar al panel de administración. Si todavía no lo ves, cerrá sesión y volvé a ingresar.",
                    Link = AdminDashboardLink,
                    CreatedAt = clock.GetUtcNow(),
                }]);
                await notifications.SaveChangesAsync(cancellationToken);
            }
        }

        user.IsAdministrator = isAdmin;
        return UpdateInvestorAccess.ToResponse(user);
    }
}
