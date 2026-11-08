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
    // withBicycle: the household has a bike (TravelModes.HasBicycle), so each trip is also measured by bike (alone or
    // with a train) and the faster one is kept.
    public static async Task<Dictionary<string, IReadOnlyList<TravelTime?>>> MeasureAsync(
        IRoutingClient routing,
        IReadOnlyList<ZoneProfile> zones,
        IReadOnlyList<UserPlace> places,
        TravelMode travelMode,
        SemaphoreSlim limiter,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, (double Latitude, double Longitude)>? origins = null,
        bool withBicycle = false)
    {
        async Task<TravelTime> MeasureTripAsync(double latitude, double longitude, UserPlace place, TravelMode routeMode)
        {
            await limiter.WaitAsync(cancellationToken);
            try
            {
                var trip = await routing.GetTravelTimeAsync(latitude, longitude, place.Latitude!.Value, place.Longitude!.Value, routeMode, cancellationToken)
                           ?? TravelModes.Estimate(routeMode, latitude, longitude, place.Latitude!.Value, place.Longitude!.Value);
                return routeMode == TravelMode.Bicycle ? trip with { ByBicycle = true } : trip;
            }
            finally
            {
                limiter.Release();
            }
        }

        async Task<TravelTime?> MeasureAsync(ZoneProfile zone, UserPlace place)
        {
            if (!place.HasLocation)
                return null;

            var (latitude, longitude) = origins is not null && origins.TryGetValue(zone.Slug, out var origin)
                ? origin
                : (zone.Latitude, zone.Longitude);

            // Five blocks or less are walked (TravelModes.WalkingDistanceThresholdKm)
            var routeMode = TravelModes.ForDistance(travelMode, latitude, longitude, place.Latitude!.Value, place.Longitude!.Value);
            var trip = await MeasureTripAsync(latitude, longitude, place, routeMode);
            if (!withBicycle || routeMode is TravelMode.Bicycle or TravelMode.Walking)
                return trip;

            var bicycleTrip = await MeasureTripAsync(latitude, longitude, place, TravelMode.Bicycle);
            return Faster(trip, bicycleTrip);
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

    // A measured trip beats an estimated one, and a trip with minutes beats one without a route.
    private static TravelTime Faster(TravelTime main, TravelTime bicycle) =>
        (main, bicycle) switch
        {
            ({ Minutes: null }, _) => bicycle,
            (_, { Minutes: null }) => main,
            ({ IsEstimated: true }, { IsEstimated: false }) => bicycle,
            ({ IsEstimated: false }, { IsEstimated: true }) => main,
            _ => bicycle.Minutes < main.Minutes ? bicycle : main,
        };
}
