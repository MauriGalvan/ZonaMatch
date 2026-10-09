using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases;

public class DeleteGroup
{
    private readonly IGroupRepository _groupRepository;

    public DeleteGroup(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid groupId, Guid userId, CancellationToken cancellationToken)
    {
        // 1. Buscamos el grupo por ID para ver si existe
        var grupo = await _groupRepository.GetByIdAsync(groupId, cancellationToken);

        if (grupo == null)
        {
            // Lanza una excepción que tu Middleware o Controlador puede atrapar para devolver un 404 Not Found
            throw new Exception("Grupo no encontrado");
        }

        // 2. Verificamos si el usuario actual es "Propietario" de este grupo
        // (Si no es el propietario, no lo dejamos borrar)
        var esPropietario = grupo.Participants.Any(p => p.UsuarioId == userId && p.Role == "Propietario");

        if (!esPropietario)
        {
            // Lanza una excepción para devolver un 403 Forbidden
            throw new Exception("No tienes permiso para eliminar este grupo");
        }

        // 3. Si todo está bien, le decimos al repositorio que lo elimine
        await _groupRepository.DeleteAsync(grupo, cancellationToken);
    }
}