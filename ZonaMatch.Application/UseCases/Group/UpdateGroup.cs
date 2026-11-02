using System;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Group;

public class UpdateGroup
{
    private readonly IGroupRepository _groupRepository;

    public UpdateGroup(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid groupId, Guid userId, UpdateGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);

        if (group == null)
            throw new NotFoundException("Grupo no encontrado");

        var isOwner = group.Participants.Any(participant => participant.UserId == userId && GroupRole.Owner == participant.Role);

        if (!isOwner)
            throw new ForbiddenException("No tienes permiso para modificar este grupo");

        group.Name = request.Name;

        await _groupRepository.UpdateAsync(group, cancellationToken);
    }
}
