using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Models;

namespace ZonaMatch.Application.Interfaces;

// Read-only site statistics for the admin panel.
public interface IAdminStatsRepository
{
    Task<AdminStatsSnapshot> GetSnapshotAsync(AdminStatsPeriod period, CancellationToken cancellationToken = default);

    // Zone rankings, each with up to `size` zones; ties go by slug.
    Task<AdminZoneActivitySnapshot> GetZoneActivityAsync(int size, CancellationToken cancellationToken = default);

    // Every group with its members, in no particular order.
    Task<IReadOnlyList<AdminGroupResponse>> ListGroupsAsync(CancellationToken cancellationToken = default);
}
