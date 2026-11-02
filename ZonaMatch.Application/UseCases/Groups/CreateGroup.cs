using System;
using System.Collections.Generic;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;


namespace ZonaMatch.Application.UseCases.Groups;

public class CreateGroup
{
    private readonly IGroupRepository _groupRepository;

    public CreateGroup(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<GroupResponse> ExecuteAsync(CreateGroupRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var newGroup = new Group
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            Participants = new List<Participant>

            {
                    new Participant
                    {
                        UserId = userId,
                        Role = GroupRole.Owner
                    }
                }
        };

        await _groupRepository.AddAsync(newGroup, cancellationToken);

        return new GroupResponse
        {
            Id = newGroup.Id,
            Name = newGroup.Name,
            CreatedAt = newGroup.CreatedAt
        };
    }
}
