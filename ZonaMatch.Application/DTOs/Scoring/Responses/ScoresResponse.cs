namespace ZonaMatch.Application.DTOs.Scoring.Responses;

// SearchExpanded: no zone around the user's places reached CalculateScores.MinimumExpectedScore, and better zones
// from the rest of the catalog were added to the ranking.
public sealed record ScoresResponse(
    string TravelMode,
    IReadOnlyList<ScoredZoneResponse> Zones,
    IReadOnlyList<ScoredZoneResponse> Recommendations,
    bool SearchExpanded = false);
