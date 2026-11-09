namespace ZonaMatch.Application.DTOs.ZoneIndicator.Responses;

public sealed record ZoneHabitabilityConditionsResponse(
    double? GoodHousingQualityPercentage,
    double? OvercrowdedHouseholdsPercentage,
    double? RentingHouseholdsPercentage,
    double? UnmetBasicNeedsPercentage,
    int? GoodHousingQualityHouseholds = null,
    int? OvercrowdedHouseholds = null,
    int? RentingHouseholds = null,
    int? UnmetBasicNeedsHouseholds = null);
