namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record AnalysisPlaceResponse(
    Guid Id,
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? Type,
    string? WhoGoes,
    string? Frequency,
    string? Priority,
    string? MaximumTravelTime,
    int Order);
