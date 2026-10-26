using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs.GroupInvitation;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.GroupInvitations;

public class CreateInvitation
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationRepository _notifications;
    private readonly IEmailService _emailService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateInvitation> _logger;

    public CreateInvitation(
        IGroupRepository groupRepository,
        IGroupInvitationRepository invitationRepository,
        IUserRepository userRepository,
        INotificationRepository notifications,
        IEmailService emailService,
        TimeProvider timeProvider,
        ILogger<CreateInvitation> logger)
    {
        _groupRepository = groupRepository;
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _notifications = notifications;
        _emailService = emailService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Le pasamos el currentUserId que sacaremos del JWT en el Controlador
    public async Task ExecuteAsync(
        CreateInvitationRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken,
        string? frontendBaseUrl = null)
    {
        // 1. Buscar el grupo y validar que exista
        var group = await _groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group == null)
        {
            throw new NotFoundException("El grupo no existe.");
        }

        // 2. Validar que el usuario que intenta invitar sea el Propietario (Owner)
        // Asumo que tienes alguna forma de saber si es el dueño, ajusta esta línea a tu lógica real de tu entidad:
        bool isOwner = group.Participants.Any(p => p.UserId == currentUserId && GroupRole.Owner == p.Role);
        if (!isOwner)
        {
            throw new ForbiddenException("Solo el propietario del grupo puede enviar invitaciones.");
        }

        // 3. Crear la entidad de invitación con el Token aleatorio
        var invitation = new GroupInvitation
        {
            GroupId = request.GroupId,
            Email = request.Email,
            Token = Guid.NewGuid(), // Este es el Guid V4 que viajará en el link
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // Le damos 7 días de validez a la invitación
        };

        var invitedUser = await _userRepository.GetByEmailAsync(
            request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (invitedUser is not null && invitedUser.Id != currentUserId)
        {
            _notifications.AddRange([
                new UserNotification
                {
                    UserId = invitedUser.Id,
                    Type = NotificationType.GroupInvitation,
                    Title = "Te invitaron a un grupo",
                    Message = $"Te invitaron a unirte a {group.Name}.",
                    Link = $"/invitations/email/{invitation.Token}",
                    CreatedAt = _timeProvider.GetUtcNow()
                }
            ]);
        }

        // 4. Guardar en la base de datos
        await _invitationRepository.AddAsync(invitation, cancellationToken);

        // 5. Enviar el correo usando tu IEmailService
        var frontendUrl = string.IsNullOrWhiteSpace(frontendBaseUrl)
            ? "http://localhost:5173"
            : frontendBaseUrl.TrimEnd('/');
        string inviteLink = $"{frontendUrl}/invitations/email/{invitation.Token}";
        string subject = $"Te invitaron al grupo {group.Name} en ZonaMatch";
        var days = Math.Max(1, (int)Math.Ceiling((invitation.ExpiresAt - DateTime.UtcNow).TotalDays));
        // The group name is typed by a user: it is encoded so it cannot inject HTML
        string body = BrandEmail.Render(new BrandEmailContent(
            Preheader: $"Sumate al grupo {group.Name} para buscar zona juntos.",
            Title: "¡Te invitaron a buscar zona en grupo!",
            Paragraphs:
            [
                $"Te invitaron a sumarte al grupo <strong>{BrandEmail.Encode(group.Name)}</strong> en ZonaMatch.",
                "Cada integrante carga lo que le importa y ZonaMatch arma un ranking de zonas que tiene en cuenta a todos.",
            ],
            Button: new BrandEmailButton("Unirme al grupo", inviteLink),
            Footnote: $"La invitación vence en {days} {(days == 1 ? "día" : "días")}. Si no esperabas este mail, podés ignorarlo."));

        await _emailService.SendEmailAsync(request.Email, subject, body, cancellationToken);

        // The recipient address is not logged on purpose
        _logger.LogInformation("User {UserId} sent an email invitation to group {GroupId}", currentUserId, group.Id);
    }

}
