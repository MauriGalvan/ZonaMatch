namespace ZonaMatch.Domain.Entities;

// Comprehensive safety evaluation result for a zone aligned with ScoringEngine.
public sealed record ZoneSafetyEvaluation(
    double Score,
    int MaxScore,
    SafetyBadge Badge,
    string Summary,
    double? CrimesPerThousandResidents,
    int? Percentile,
    double? CityAverageRate,
    int TotalCrimes,
    string DominantCategoryText,
    IReadOnlyList<SafetyBreakdownItem> Breakdown,
    int RoadIncidents);
