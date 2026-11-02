namespace ZonaMatch.Application.DTOs.Scoring.Responses;

public sealed record ScoresResponse(
    string TravelMode,
    IReadOnlyList<ScoredZoneResponse> Zones,
    IReadOnlyList<ScoredZoneResponse> Recommendations);
