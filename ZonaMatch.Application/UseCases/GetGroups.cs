using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases;

public class GetGroups
{
    private readonly IGroupRepository _groupRepository;
    public GetGroups(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }
    public async Task<GroupListResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var groups = await _groupRepository.GetByUserCreatorIdAsync(userId, cancellationToken);
        // Mapear a DTOs
        var response = new GroupListResponse
        {
            Groups = groups.Select(g => new GroupResponse{Id = g.Id, Name = g.Name}).ToList()
        };

        return response;
    }
}
