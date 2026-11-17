using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Investor;

// The user asks for the investor profile and every administrator is notified. Asking again changes nothing: a pending
// request keeps its date and granted access stays granted.
public sealed class RequestInvestorAccess(IUserRepository users, INotificationRepository notifications, TimeProvider clock)
{
    public const string AdminUsersLink = "/admin/usuarios";

    public async Task<InvestorAccessResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (user.InvestorAccess is not null)
            return InvestorAccessStatus.ToResponse(user);

        var now = clock.GetUtcNow();
        await users.SetInvestorAccessAsync(userId, InvestorAccess.Requested, now, null, cancellationToken);

        var administrators = await users.GetAdministratorIdsAsync(cancellationToken);
        notifications.AddRange(administrators.Select(administratorId => new UserNotification
        {
            UserId = administratorId,
            Type = NotificationType.InvestorAccessRequested,
            Title = "Solicitud de perfil de inversor",
            Message = $"{user.Email} pidió acceso al perfil de inversor.",
            Link = AdminUsersLink,
            CreatedAt = now,
        }));
        await notifications.SaveChangesAsync(cancellationToken);

        return new InvestorAccessResponse(InvestorAccessStatus.Requested, now, null);
    }
}
