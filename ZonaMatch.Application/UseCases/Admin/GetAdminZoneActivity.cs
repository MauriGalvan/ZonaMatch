using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;

namespace ZonaMatch.Application.UseCases.Admin;

// Zone rankings for the admin Zones page: saved the most and the least, and with the most reviews, questions and
// contributions. Each ranking has up to RankingSize zones.
public sealed class GetAdminZoneActivity(IAdminStatsRepository stats, IZoneProfilesRepository zones)
{
    public const int RankingSize = 10;

    public async Task<AdminZoneActivityResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var activity = await stats.GetZoneActivityAsync(RankingSize, cancellationToken);
        var zoneNames = await AdminLookups.ZoneNamesAsync(zones, cancellationToken);

        List<AdminZoneCount> Named(IReadOnlyList<ZoneActivityCount> counts) =>
            counts.Select(count => new AdminZoneCount(count.ZoneSlug, zoneNames.GetValueOrDefault(count.ZoneSlug) ?? count.ZoneSlug, count.Count))
                .ToList();

        return new AdminZoneActivityResponse(
            Named(activity.MostSaved),
            Named(activity.LeastSaved),
            Named(activity.MostReviewed),
            Named(activity.MostAsked),
            Named(activity.MostContributed));
    }
}
