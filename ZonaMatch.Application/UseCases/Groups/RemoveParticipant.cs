using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Groups;

public class RemoveParticipant
{
    private readonly IGroupRepository _groupRepository;

    public RemoveParticipant(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid groupId, Guid userIdToRemove, Guid currentUserId, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group == null)
        {
            throw new NotFoundException("El grupo no existe.");
        }

        var participantToRemove = group.Participants.FirstOrDefault(p => p.UserId == userIdToRemove);
        if (participantToRemove == null)
        {
            throw new NotFoundException("El usuario no es miembro de este grupo.");
        }

        var isSelfRemoval = currentUserId == userIdToRemove;
        var isCurrentUserOwner = group.Participants.Any(p => p.UserId == currentUserId && p.Role == GroupRole.Owner);

        if (!isSelfRemoval && !isCurrentUserOwner)
        {
            throw new ForbiddenException("No tienes permisos para eliminar a este participante.");
        }

        if (isSelfRemoval && participantToRemove.Role == GroupRole.Owner)
        {
            throw new ConflictException("El propietario no puede abandonar el grupo. Elimina el grupo para dejarlo.");
        }

        await _groupRepository.RemoveParticipantAsync(participantToRemove, cancellationToken);
    }
}
