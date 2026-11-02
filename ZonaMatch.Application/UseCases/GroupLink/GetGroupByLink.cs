using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.GroupLink.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.GroupLink;

public class GetGroupByLink
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupByLink(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<GroupLinkDetailsResponse> ExecuteAsync(Guid inviteToken, CancellationToken cancellationToken)
    {
        // Load the group associated with the invitation token.
        var group = await _groupRepository.GetByInviteTokenAsync(inviteToken, cancellationToken);

        if (group == null)
        {
            throw new NotFoundException("El enlace de invitación no es válido o ha sido revocado.");
        }

        return new GroupLinkDetailsResponse
        {
            GroupId = group.Id,
            GroupName = group.Name
        };
    }
}
