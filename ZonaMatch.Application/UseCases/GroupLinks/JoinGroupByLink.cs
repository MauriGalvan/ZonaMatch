using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.GroupLinks;

public class JoinGroupByLink
{
    private readonly IGroupRepository _groupRepository;

    public JoinGroupByLink(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid inviteToken, Guid currentUserId, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByInviteTokenAsync(inviteToken, cancellationToken);

        if (group == null)
        {
            throw new Exception("El enlace de invitación no es válido o ha sido revocado.");
        }

        // Validar si ya está adentro
        bool alreadyMember = group.Participants.Any(p => p.UserId == currentUserId);
        if (alreadyMember)
        {
            throw new Exception("Ya eres miembro de este grupo.");
        }

        // Crear el objeto participante
        var newParticipant = new Participant
        {
            Id = Guid.CreateVersion7(),
            GroupId = group.Id,
            UserId = currentUserId,
            Role = GroupRole.Guest
        };

        // Insertar directamente al participante en lugar de intentar actualizar el grupo completo
        await _groupRepository.AddParticipantAsync(newParticipant, cancellationToken);
    }
}