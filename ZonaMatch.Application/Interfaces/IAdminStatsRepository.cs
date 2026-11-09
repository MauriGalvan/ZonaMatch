using ZonaMatch.Application.Models;

namespace ZonaMatch.Application.Interfaces;

// Read-only site statistics for the admin panel.
public interface IAdminStatsRepository
{
    Task<AdminStatsSnapshot> GetSnapshotAsync(AdminStatsPeriod period, CancellationToken cancellationToken = default);
}
