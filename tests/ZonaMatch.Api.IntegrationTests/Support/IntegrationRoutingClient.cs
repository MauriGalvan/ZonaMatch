using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Api.IntegrationTests.Support;

internal sealed class IntegrationRoutingClient : IRoutingClient
{
    public Task<TravelTime?> GetTravelTimeAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        TravelMode mode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<TravelTime?>(new TravelTime(15, IsEstimated: false, DistanceKm: 2));
}
