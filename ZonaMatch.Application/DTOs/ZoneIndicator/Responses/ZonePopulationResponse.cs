namespace ZonaMatch.Application.DTOs.ZoneIndicator.Responses;

public sealed record ZonePopulationResponse(
    int Total,
    double? PopulationDensityPerKm2,
    int Households,
    int Dwellings,
    double? Under15Percentage,
    double? Age15To64Percentage,
    double? Over65Percentage);
