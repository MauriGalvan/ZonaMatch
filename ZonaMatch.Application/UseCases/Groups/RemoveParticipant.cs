using System;
using System.Collections.Generic;
using System.Text;
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
        // 1. Obtener el grupo (Asegúrate de que este método tenga el .Include(g => g.Participants) en el repo)
        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group == null)
        {
            throw new Exception("El grupo no existe."); // O NotFoundException
        }

        // 2. Buscar al participante que queremos eliminar
        var participantToRemove = group.Participants.FirstOrDefault(p => p.UserId == userIdToRemove);
        if (participantToRemove == null)
        {
            throw new Exception("El usuario no es miembro de este grupo."); // O NotFoundException
        }

        // 3. Lógica de Permisos
        bool isSelfRemoval = currentUserId == userIdToRemove;
        bool isCurrentUserOwner = group.Participants.Any(p => p.UserId == currentUserId && p.Role == GroupRole.Owner);

        if (!isSelfRemoval && !isCurrentUserOwner)
        {
            throw new Exception("No tienes permisos para eliminar a este participante."); // O ForbiddenException
        }

        // 4. Regla de negocio opcional: El propietario no puede abandonar el grupo si es el único (o sin ceder el ownership)
        if (isSelfRemoval && participantToRemove.Role == GroupRole.Owner)
        {
            // Opcional: Validar si hay más propietarios o impedir que se vaya directamente
            throw new Exception("El propietario no puede abandonar el grupo. Elimina el grupo para dejarlo.");
        }

        // 5. Eliminar el participante de la base de datos
        await _groupRepository.RemoveParticipantAsync(participantToRemove, cancellationToken);
    }
}
