using ZonaMatch.Application.DTOs.ZoneIndicator.Responses;

namespace ZonaMatch.Application.DTOs.Zone.Responses;

// Detailed safety evaluation, qualitative badge, benchmark comparison and crime breakdown for a zone.
public sealed record ZoneSafetyResponse(
    double Score,
    int MaxScore,
    SafetyBadgeResponse Badge,
    string Summary,
    double? CrimesPerThousandResidents,
    double? CityAverageRate,
    int TotalCrimes,
    string DominantCategoryText,
    IReadOnlyList<SafetyBreakdownItemResponse> Breakdown,
    SafetyRoadIncidentsResponse RoadIncidents,
    int? Percentile = null,
    int Year = 0,
    string Source = "mapa-delito",
    string? Scope = null,
    int Crimes = 0,
    double? CrimesPer1000Residents = null,
    int Robberies = 0,
    int Thefts = 0,
    int Injuries = 0,
    int Homicides = 0,
    int Threats = 0,
    IndicatorReferenceResponse? Reference = null);
