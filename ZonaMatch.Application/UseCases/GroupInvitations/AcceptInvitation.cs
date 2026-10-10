using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.GroupInvitations;

public class AcceptInvitation
{
    private readonly IGroupInvitationRepository _invitationRepository;
    private readonly IGroupRepository _groupRepository;

    public AcceptInvitation(
        IGroupInvitationRepository invitationRepository,
        IGroupRepository groupRepository)
    {
        _invitationRepository = invitationRepository;
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid token, Guid currentUserId, CancellationToken cancellationToken)
    {
        // 1. Buscar la invitación
        var invitation = await _invitationRepository.GetByTokenAsync(token, cancellationToken);
        if (invitation == null)
        {
            throw new Exception("Invitación no encontrada.");
        }

        // 2. Validar que siga pendiente
        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new Exception($"La invitación ya no es válida (Estado: {invitation.Status}).");
        }

        // 3. Validar que no haya expirado
        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            // Si expiró, le actualizamos el estado en la BD para que quede el registro
            invitation.Status = InvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation, cancellationToken);
            throw new Exception("La invitación ha expirado.");
        }

        // 4. Buscar el grupo y validar que el usuario no esté ya adentro
        var group = await _groupRepository.GetByIdAsync(invitation.GroupId, cancellationToken);
        if (group == null) throw new Exception("El grupo ya no existe.");

        bool alreadyMember = group.Participants.Any(p => p.UserId == currentUserId);
        if (alreadyMember)
        {
            throw new Exception("Ya eres miembro de este grupo.");
        }

        // 5. Agregar al usuario como participante (Ajusta el "Role" según cómo lo manejes en tu app)
        var newParticipant = new Participant
        {
            Id = Guid.CreateVersion7(), // Si usas V7 en Participant
            GroupId = group.Id,
            UserId = currentUserId,
            Role = "Member" // o "Invitado", "Member", etc.
        };
        group.Participants.Add(newParticipant);

        // 6. Cambiar estado de la invitación a Aceptada
        invitation.Status = InvitationStatus.Accepted;

        // 7. Guardar los cambios
        await _groupRepository.UpdateAsync(group, cancellationToken);
        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
    }
}
