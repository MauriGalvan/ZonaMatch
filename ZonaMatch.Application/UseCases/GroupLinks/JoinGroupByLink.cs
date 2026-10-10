using System;
using System.Collections.Generic;
using System.Text;
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

        // Agregarlo a la lista de participantes
        var newParticipant = new Participant
        {
            Id = Guid.CreateVersion7(), // Si usas V7
            GroupId = group.Id,
            UserId = currentUserId,
            Role = GroupRole.Guest
        };

        group.Participants.Add(newParticipant);

        await _groupRepository.UpdateAsync(group, cancellationToken);
    }
}
