using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// Every group for the admin statistics: newest first, each with its owner first and the other members by email.
public sealed class ListAdminGroups(IAdminStatsRepository stats)
{
    public async Task<IReadOnlyList<AdminGroupResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var groups = await stats.ListGroupsAsync(cancellationToken);

        return groups
            .OrderByDescending(group => group.CreatedAt)
            .ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => group with
            {
                Members = group.Members
                    .OrderByDescending(member => member.IsOwner)
                    .ThenBy(member => member.Email, StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();
    }
}
