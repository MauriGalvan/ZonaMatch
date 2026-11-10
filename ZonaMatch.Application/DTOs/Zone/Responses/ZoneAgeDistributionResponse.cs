namespace ZonaMatch.Application.DTOs.Zone.Responses;

public sealed record ZoneAgeDistributionResponse(
    double? Age0To4Percentage,
    double? Age5To14Percentage,
    double? Age15To29Percentage,
    double? Age30To44Percentage,
    double? Age45To64Percentage,
    double? Over65Percentage);
