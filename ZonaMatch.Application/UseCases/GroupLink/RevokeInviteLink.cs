using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.GroupLink;

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
        if (group == null) throw new NotFoundException("El grupo no existe.");

        // Verify that the user revoking the link owns the group.
        bool isOwner = group.Participants.Any(p => p.UserId == currentUserId && p.Role == GroupRole.Owner);
        if (!isOwner)
        {
            throw new ForbiddenException("Solo el propietario puede revocar el enlace de invitación.");
        }

        // Generate a new UUID to revoke the old link; its token will no longer match the group.
        group.InviteToken = Guid.NewGuid();

        await _groupRepository.UpdateAsync(group, cancellationToken);

        // Return the new token so the frontend can update the displayed link.
        return group.InviteToken;
    }
}
