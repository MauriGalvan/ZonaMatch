using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Application.UseCases.GroupInvitation;

public class AcceptInvitation
{
    private readonly IGroupInvitationRepository _invitationRepository;
    private readonly IGroupRepository _groupRepository;

    public AcceptInvitation(
        IGroupInvitationRepository invitationRepository,
        IGroupRepository groupRepository)
    {
        _invitationRepository = invitationRepository;
        _groupRepository = groupRepository;
    }

    public async Task ExecuteAsync(Guid token, Guid currentUserId, CancellationToken cancellationToken)
    {
        // 1. Find the invitation.
        var invitation = await _invitationRepository.GetByTokenAsync(token, cancellationToken);
        if (invitation == null)
        {
            throw new NotFoundException("Invitación no encontrada.");
        }

        // 2. Ensure it is still pending.
        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new BadRequestException($"La invitación ya no es válida (Estado: {invitation.Status}).");
        }

        // 3. Ensure it has not expired.
        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            // Persist the expired status so the invitation record reflects its final state.
            invitation.Status = InvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation, cancellationToken);
            throw new BadRequestException("La invitación ha expirado.");
        }

        // 4. Find the group and ensure the user is not already a member.
        var group = await _groupRepository.GetByIdAsync(invitation.GroupId, cancellationToken);
        if (group == null) throw new NotFoundException("El grupo ya no existe.");

        bool alreadyMember = group.Participants.Any(p => p.UserId == currentUserId);
        if (alreadyMember)
        {
            throw new ConflictException("Ya eres miembro de este grupo.");
        }

        if (group.Participants.Count >= DomainGroup.MaximumParticipants)
        {
            throw new ConflictException("El grupo ya alcanzó el máximo de 5 participantes.");
        }

        // 5. Add the user as a participant, using the role configured for this operation.
        var newParticipant = new Participant
        {
            GroupId = group.Id,
            UserId = currentUserId,
            Role = GroupRole.Guest
        };
        group.Participants.Add(newParticipant);

        // 6. Mark the invitation as accepted.
        invitation.Status = InvitationStatus.Accepted;

        // 7. Save the changes.
        await _groupRepository.AddParticipantAsync(newParticipant, cancellationToken);
        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
    }
}
