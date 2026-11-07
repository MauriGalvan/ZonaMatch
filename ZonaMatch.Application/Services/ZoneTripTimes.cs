using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.Services;

// Travel time from each zone to each place. Shared by the ranking, the group ranking and the analysis comparison,
// so the three measure the same trip the same way.
public static class ZoneTripTimes
{
    // Routing requests in flight at the same time
    public const int ConcurrentTrips = 6;

    // origins: starting points chosen inside a zone instead of its center (only the individual ranking has them).
    // limiter: shared by every call of one operation, so the whole operation keeps ConcurrentTrips requests at most.
    public static async Task<Dictionary<string, IReadOnlyList<TravelTime?>>> MeasureAsync(
        IRoutingClient routing,
        IReadOnlyList<ZoneProfile> zones,
        IReadOnlyList<UserPlace> places,
        TravelMode travelMode,
        SemaphoreSlim limiter,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, (double Latitude, double Longitude)>? origins = null)
    {
        async Task<TravelTime?> MeasureAsync(ZoneProfile zone, UserPlace place)
        {
            if (!place.HasLocation)
                return null;

            var (latitude, longitude) = origins is not null && origins.TryGetValue(zone.Slug, out var origin)
                ? origin
                : (zone.Latitude, zone.Longitude);

            await limiter.WaitAsync(cancellationToken);
            try
            {
                // Five blocks or less are walked (TravelModes.WalkingDistanceThresholdKm)
                var routeMode = TravelModes.ForDistance(travelMode, latitude, longitude, place.Latitude!.Value, place.Longitude!.Value);
                return await routing.GetTravelTimeAsync(latitude, longitude, place.Latitude!.Value, place.Longitude!.Value, routeMode, cancellationToken)
                       ?? TravelModes.Estimate(routeMode, latitude, longitude, place.Latitude!.Value, place.Longitude!.Value);
            }
            finally
            {
                limiter.Release();
            }
        }

        var byZone = zones
            .DistinctBy(zone => zone.Slug)
            .Select(async zone => (zone.Slug, Times: await Task.WhenAll(places.Select(place => MeasureAsync(zone, place)))));

        var results = await Task.WhenAll(byZone);
        return results.ToDictionary(
            result => result.Slug,
            result => (IReadOnlyList<TravelTime?>)result.Times,
            StringComparer.Ordinal);
    }
}
