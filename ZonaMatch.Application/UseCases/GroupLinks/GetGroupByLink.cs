using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.GroupLink;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.GroupLinks;

public class GetGroupByLink
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupByLink(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<GroupLinkDetailsResponse> ExecuteAsync(Guid inviteToken, CancellationToken cancellationToken)
    {
        // Usamos el método que creamos en la Tarjeta 2
        var group = await _groupRepository.GetByInviteTokenAsync(inviteToken, cancellationToken);

        if (group == null)
        {
            throw new Exception("El enlace de invitación no es válido o ha sido revocado.");
        }

        return new GroupLinkDetailsResponse
        {
            GroupId = group.Id,
            GroupName = group.Name
        };
    }
}
