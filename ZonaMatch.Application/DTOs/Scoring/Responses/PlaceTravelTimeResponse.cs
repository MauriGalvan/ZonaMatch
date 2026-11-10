namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>Route distance, not straight-line distance. Null when the complete route is unknown.
/// ByBicycle: the trip is by bike, alone or with a train, because it was faster for a household with a bike.</summary>
public sealed record PlaceTravelTimeResponse(
    string Name,
    int? Minutes,
    int MaximumMinutes,
    bool IsEstimated,
    double? DistanceKm = null,
    bool ByBicycle = false);
