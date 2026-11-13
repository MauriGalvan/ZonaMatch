using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Investor;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Admin;

// An administrator grants or removes the investor profile, whether or not the user asked for it. Granting notifies the
// user; removing it keeps the investor profile saved, so granting it again brings it back.
public sealed class UpdateInvestorAccess(IUserRepository users, INotificationRepository notifications, TimeProvider clock)
{
    public const string InvestorProfileLink = "/perfil?tipo=inversor";

    public async Task<AdminUserResponse> ExecuteAsync(Guid userId, bool granted, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (granted && !user.HasInvestorAccess)
        {
            var now = clock.GetUtcNow();
            user.InvestorAccess = InvestorAccess.Granted;
            user.InvestorAccessGrantedAt = now;
            await users.SetInvestorAccessAsync(userId, user.InvestorAccess, user.InvestorAccessRequestedAt, now, cancellationToken);

            notifications.AddRange([new UserNotification
            {
                UserId = userId,
                Type = NotificationType.InvestorAccessGranted,
                Title = "Perfil de inversor habilitado",
                Message = "Ya podés configurar tu perfil de inversor y analizar dónde conviene invertir.",
                Link = InvestorProfileLink,
                CreatedAt = now,
            }]);
            await notifications.SaveChangesAsync(cancellationToken);
        }
        else if (!granted && user.InvestorAccess is not null)
        {
            user.InvestorAccess = null;
            user.InvestorAccessRequestedAt = null;
            user.InvestorAccessGrantedAt = null;
            await users.SetInvestorAccessAsync(userId, null, null, null, cancellationToken);
        }

        return ToResponse(user);
    }

    public static AdminUserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.DateOfBirth, user.IsAdministrator, InvestorAccessStatus.Of(user), user.InvestorAccessRequestedAt);
}
