namespace ZonaMatch.Application.DTOs.ZoneRecommendation.Responses;

public sealed record ZoneProfileRecommendationsResponse(
    IReadOnlyList<ProfileRecommendationResponse> Places,
    bool Truncated);
