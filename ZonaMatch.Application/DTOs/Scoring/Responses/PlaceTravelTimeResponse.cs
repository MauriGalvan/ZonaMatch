namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>Route distance, not straight-line distance. Null when the complete route is unknown.</summary>
public sealed record PlaceTravelTimeResponse(
    string Name,
    int? Minutes,
    int MaximumMinutes,
    bool IsEstimated,
    double? DistanceKm = null);
