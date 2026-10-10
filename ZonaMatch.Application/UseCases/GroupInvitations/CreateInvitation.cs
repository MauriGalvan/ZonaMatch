using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.GroupInvitation;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.GroupInvitations;

public class CreateInvitation
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupInvitationRepository _invitationRepository;
    private readonly IEmailService _emailService;

    public CreateInvitation(
        IGroupRepository groupRepository,
        IGroupInvitationRepository invitationRepository,
        IEmailService emailService)
    {
        _groupRepository = groupRepository;
        _invitationRepository = invitationRepository;
        _emailService = emailService;
    }

    // Le pasamos el currentUserId que sacaremos del JWT en el Controlador
    public async Task ExecuteAsync(CreateInvitationRequest request, Guid currentUserId, CancellationToken cancellationToken)
    {
        // 1. Buscar el grupo y validar que exista
        var group = await _groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group == null)
        {
            throw new Exception("El grupo no existe."); // O tu excepción personalizada (NotFoundException)
        }

        // 2. Validar que el usuario que intenta invitar sea el Propietario (Owner)
        // Asumo que tienes alguna forma de saber si es el dueño, ajusta esta línea a tu lógica real de tu entidad:
        bool isOwner = group.Participants.Any(p => p.UserId == currentUserId && p.Role == GroupRole.Owner);
        if (!isOwner)
        {
            throw new Exception("Solo el propietario del grupo puede enviar invitaciones."); // (ForbiddenException)
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

        // 4. Guardar en la base de datos
        await _invitationRepository.AddAsync(invitation, cancellationToken);

        // 5. Enviar el correo usando tu IEmailService
        // Más adelante cambiaremos el "localhost" por la URL de tu frontend real
        string inviteLink = $"http://localhost:3000/invitations/email/{invitation.Token}";
        string subject = $"¡Te han invitado al grupo {group.Name} en ZonaMatch!";

        // Armamos un mini HTML para que el correo se vea más presentable
        string body = $@"
                <h3>¡Hola!</h3>
                <p>Te han invitado a unirte al grupo <strong>{group.Name}</strong>.</p>
                <p>Haz clic en el siguiente enlace para aceptar la invitación:</p>
                <a href='{inviteLink}'>Unirme al Grupo</a>
                <br/><br/>
                <p>Este enlace expirará en 7 días.</p>
            ";

        await _emailService.SendEmailAsync(request.Email, subject, body, cancellationToken);
    }

}
