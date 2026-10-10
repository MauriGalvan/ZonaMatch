using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.GroupLinks;

public class RevokeInviteLink
{
    private readonly IGroupRepository _groupRepository;

    public RevokeInviteLink(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<Guid> ExecuteAsync(Guid groupId, Guid currentUserId, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group == null) throw new Exception("El grupo no existe.");

        // Validar que el que revoca es el Propietario
        bool isOwner = group.Participants.Any(p => p.UserId == currentUserId && p.Role == "Owner");
        if (!isOwner)
        {
            throw new Exception("Solo el propietario puede revocar el enlace de invitación.");
        }

        // La magia está acá: simplemente generamos un nuevo Guid. 
        // El link viejo ya no encontrará nada en la base de datos.
        group.InviteToken = Guid.NewGuid();

        await _groupRepository.UpdateAsync(group, cancellationToken);

        // Devolvemos el nuevo token para que el frontend lo actualice en pantalla
        return group.InviteToken;
    }
}
