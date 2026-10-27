namespace ZonaMatch.Application.DTOs.ZoneRecommendations.Responses;

public sealed record ZoneProfileRecommendationsResponse(
    IReadOnlyList<ProfileRecommendationResponse> Places,
    bool Truncated);
