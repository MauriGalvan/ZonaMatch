namespace ZonaMatch.Application.DTOs.Geolocation.Responses;

public sealed record SchoolResponse(
    string NaturalKey,
    string Name,
    string? Level,
    string? Sector,
    string? Address,
    string? Locality,
    double Latitude,
    double Longitude,
    double? DistanceMeters = null);
