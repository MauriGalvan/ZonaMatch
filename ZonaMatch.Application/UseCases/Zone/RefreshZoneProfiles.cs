using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Zone;

// Rebuilds the zone profiles the ranking uses after an ETL loads new data (market, census, crime...), without
// restarting the API. Each API instance keeps its own copy, so with several instances each one has to be refreshed.
public sealed class RefreshZoneProfiles(IZoneProfilesRepository zoneProfiles)
{
    public async Task<ZoneProfilesRefreshResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await zoneProfiles.InvalidateAsync(cancellationToken);
        var profiles = await zoneProfiles.ListAsync(cancellationToken);
        return new ZoneProfilesRefreshResponse(profiles.Count);
    }
}
