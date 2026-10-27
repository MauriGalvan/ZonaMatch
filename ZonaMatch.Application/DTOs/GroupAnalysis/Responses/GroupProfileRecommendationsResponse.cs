namespace ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

public sealed record GroupProfileRecommendationsResponse(
    IReadOnlyList<string> AgeRanges,
    bool HasPets,
    bool HasVehicle,
    bool HasBicycle);
