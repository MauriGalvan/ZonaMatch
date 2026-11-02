using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.GroupInvitation;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
namespace ZonaMatch.Application.UseCases.GroupInvitations;

public class GetInvitationByToken
{
    private readonly IGroupInvitationRepository _invitationRepository;

    public GetInvitationByToken(IGroupInvitationRepository invitationRepository)
    {
        _invitationRepository = invitationRepository;
    }

    public async Task<InvitationDetailsResponse> ExecuteAsync(Guid token, CancellationToken cancellationToken)
    {
        var invitation = await _invitationRepository.GetByTokenAsync(token, cancellationToken);

        if (invitation == null)
        {
            throw new NotFoundException("La invitación no existe.");
        }

        return new InvitationDetailsResponse
        {
            GroupId = invitation.GroupId,
            // The repository includes the related group with Include(i => i.Group).
            GroupName = invitation.Group.Name,
            Status = invitation.Status.ToString(),
            ExpiresAt = invitation.ExpiresAt
        };
    }
}
