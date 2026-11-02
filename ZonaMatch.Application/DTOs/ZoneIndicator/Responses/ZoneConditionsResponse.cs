namespace ZonaMatch.Application.DTOs.ZoneIndicator.Responses;

public sealed record ZoneConditionsResponse(
    double? HouseholdsWithUnmetBasicNeedsPercentage,
    double? OvercrowdedHouseholdsPercentage,
    double? RentingHouseholdsPercentage,
    double? HealthCoveragePercentage,
    double? UniversityEducationPercentage);
