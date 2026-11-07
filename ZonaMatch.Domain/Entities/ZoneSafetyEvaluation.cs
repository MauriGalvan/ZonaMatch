namespace ZonaMatch.Domain.Entities;

// Comprehensive safety evaluation result for a zone.
public sealed record ZoneSafetyEvaluation(
    int Score,
    int MaxScore,
    SafetyBadge Badge,
    string Summary,
    double? CrimesPerThousandResidents,
    double? CityAverageRate,
    int TotalCrimes,
    string DominantCategoryText,
    IReadOnlyList<SafetyBreakdownItem> Breakdown,
    int RoadIncidents);
