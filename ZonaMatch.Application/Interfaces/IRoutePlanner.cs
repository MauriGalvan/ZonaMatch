using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.Interfaces
{
    // Plans the fastest trip between two points (MOTIS), with every leg of it.
    public interface IRoutePlanner
    {
        // Null when there is no trip for that mode. Throws ServiceUnavailableException when the routing service
        // does not answer.
        Task<RoutePlan?> PlanAsync(
            double fromLatitude, double fromLongitude, double toLatitude, double toLongitude, ModoViaje mode,
            CancellationToken cancellationToken = default);
    }

    public sealed record RoutePlan(int DurationSeconds, IReadOnlyList<RouteLeg> Legs);

    // Mode: walk, bike, car, bus, train, subway, tram or ferry. Line data is only set for public transport legs.
    public sealed record RouteLeg(
        string Mode,
        int DurationSeconds,
        double? DistanceMeters,
        string? FromName,
        string? ToName,
        string? LineName,
        string? Headsign,
        string? AgencyName,
        string? Color,
        IReadOnlyList<RoutePoint> Path);

    public readonly record struct RoutePoint(double Latitude, double Longitude);
}
