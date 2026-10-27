using System;
using System.Collections.Generic;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Groups;

public class GetGroups
{
    private readonly IGroupRepository _groupRepository;
    public GetGroups(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }
    public async Task<GroupListResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var groups = await _groupRepository.GetByParticipantUserIdAsync(userId, cancellationToken);
        // Map groups to the response contract.
        var response = new GroupListResponse
        {
            Groups = groups.Select(group => new GroupResponse
            {
                Id = group.Id,
                Name = group.Name,
                IsOwner = group.Participants.Any(participant =>
                    participant.UserId == userId && participant.Role == GroupRole.Owner)
            }).ToList()
        };

        return response;
    }
}
