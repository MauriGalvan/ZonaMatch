namespace ZonaMatch.Application.DTOs.Zone.Responses;

public sealed record ZonePopulationResponse(
    int Total,
    int Households,
    int Dwellings,
    double? AverageAge,
    DemographicBadgeResponse? AverageAgeBadge,
    double? AverageHouseholdSize,
    DemographicBadgeResponse? AverageHouseholdSizeBadge,
    double? SinglePersonHouseholdsPercentage,
    DemographicBadgeResponse? SinglePersonHouseholdsBadge,
    DemographicBadgeResponse? ChildrenUnder15Badge,
    DemographicBadgeResponse? SeniorsOver65Badge,
    ZoneAgeDistributionResponse AgeDistribution,
    double? PopulationDensityPerKm2 = null);
