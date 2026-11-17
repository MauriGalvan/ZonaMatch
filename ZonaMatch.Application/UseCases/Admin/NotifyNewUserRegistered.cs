using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Admin;

// Tells every administrator that a new account was created, with a link to the admin user list. One notification per
// account.
public sealed class NotifyNewUserRegistered(IUserRepository users, INotificationRepository notifications, TimeProvider clock)
{
    public const string Title = "Nuevo usuario";
    public const string UserListLink = "/admin/usuarios";

    public async Task ExecuteAsync(User newUser, CancellationToken cancellationToken = default)
    {
        var administratorIds = (await users.GetAdministratorIdsAsync(cancellationToken)).Where(id => id != newUser.Id).ToList();
        if (administratorIds.Count == 0)
            return;

        var now = clock.GetUtcNow();
        notifications.AddRange(administratorIds.Select(administratorId => new UserNotification
        {
            UserId = administratorId,
            Type = NotificationType.NewUserRegistered,
            Title = Title,
            Message = $"{newUser.Email} creó una cuenta. Miralo en la lista de usuarios.",
            Link = UserListLink,
            CreatedAt = now,
        }));
        await notifications.SaveChangesAsync(cancellationToken);
    }
}
