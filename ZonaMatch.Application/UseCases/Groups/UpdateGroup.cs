using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.Group;
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
            throw new Exception("Grupo no encontrado");

        // Verificamos si el usuario es propietario
        var esPropietario = grupo.Participants.Any(p => p.UserId == userId && GroupRole.Owner == p.Role);

        if (!esPropietario)
            throw new Exception("No tienes permiso para modificar este grupo");

        // Modificamos los datos
        grupo.Name = request.Name;

        // Guardamos en la base de datos
        await _groupRepository.UpdateAsync(grupo, cancellationToken);
    }
}
