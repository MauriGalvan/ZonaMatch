namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record ComparedPlaceTravelTimeResponse(
    string Name,
    int? Minutes,
    int MaximumMinutes,
    bool IsEstimated,
    double? DistanceKm = null,
    bool ByBicycle = false);
