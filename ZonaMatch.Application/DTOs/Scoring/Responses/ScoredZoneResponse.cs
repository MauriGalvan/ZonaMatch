namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>Score is null when no criterion has data for the zone.</summary>
public sealed record ScoredZoneResponse(
    string Slug,
    string Name,
    int? Score,
    IReadOnlyList<ScoreComponentResponse> Components,
    IReadOnlyList<ScoreLimitResponse> Limits,
    IReadOnlyList<PlaceTravelTimeResponse> Places,
    RentResponse? Rent,
    SaleResponse? Sale = null);
