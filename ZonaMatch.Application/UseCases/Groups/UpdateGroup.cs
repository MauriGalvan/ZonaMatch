using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Groups;

public class UpdateGroup
{
    private readonly IGroupRepository _groupRepository;

    public UpdateGroup(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid groupId, Guid userId, UpdateGroupRequest request, CancellationToken cancellationToken)
    {
        var grupo = await _groupRepository.GetByIdAsync(groupId, cancellationToken);

        if (grupo == null)
            throw new NotFoundException("Grupo no encontrado");

        var esPropietario = grupo.Participants.Any(p => p.UserId == userId && GroupRole.Owner == p.Role);

        if (!esPropietario)
            throw new ForbiddenException("No tienes permiso para modificar este grupo");

        grupo.Name = request.Name;

        await _groupRepository.UpdateAsync(grupo, cancellationToken);
    }
}
