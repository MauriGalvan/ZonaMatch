using System;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Groups;

public class DeleteGroup
{
    private readonly IGroupRepository _groupRepository;

    public DeleteGroup(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid groupId, Guid userId, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);

        if (group == null)
        {
            throw new NotFoundException("Grupo no encontrado");
        }

        var isOwner = group.Participants.Any(participant => participant.UserId == userId && GroupRole.Owner == participant.Role);

        if (!isOwner)
        {
            throw new ForbiddenException("No tienes permiso para eliminar este grupo");
        }

        await _groupRepository.DeleteAsync(group, cancellationToken);
    }
}
