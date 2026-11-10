namespace ZonaMatch.Application.DTOs.ZoneIndicator.Responses;

public sealed record ZoneInfrastructureConditionsResponse(
    double? PipedWaterPercentage,
    double? SewerPercentage,
    double? GasNetworkPercentage,
    int? PipedWaterHouseholds = null,
    int? SewerHouseholds = null,
    int? GasNetworkHouseholds = null);
