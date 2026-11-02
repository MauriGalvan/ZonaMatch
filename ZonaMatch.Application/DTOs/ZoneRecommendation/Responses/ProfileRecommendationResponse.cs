namespace ZonaMatch.Application.DTOs.ZoneRecommendation.Responses;

public sealed record ProfileRecommendationResponse(
    string Id,
    string Name,
    string Category,
    double Latitude,
    double Longitude,
    double DistanceMeters);
