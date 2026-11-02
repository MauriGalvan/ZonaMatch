using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.Interfaces
{
    // Travel duration between two points (MOTIS).
    public interface IRoutingClient
    {
        // Travel time in minutes on a weekday morning; null when no route is available.
        // If the routing service is unavailable, the caller decides whether to estimate.
        Task<TravelTime?> GetTravelTimeAsync(
            double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino, TravelMode modo,
            CancellationToken cancellationToken = default);
    }
}
