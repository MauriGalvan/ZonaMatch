namespace ZonaMatch.Domain.Entities;

// Evaluates demographic indicators of a zone against a reference city (such as CABA),
// determining qualitative badges and visual variants for the user interface.
public static class ZoneDemographicBenchmark
{
    private const double AgeDeltaThresholdYears = 1.0;
    private const double HouseholdSizeDeltaThreshold = 0.1;
    private const double SinglePersonPercentageDeltaThreshold = 3.0;
    private const double ChildrenPercentageDeltaThreshold = 2.0;
    private const double SeniorsPercentageDeltaThreshold = 2.0;

    public static DemographicBadge? EvaluateAverageAge(double? zoneAverageAge, double? cityAverageAge)
    {
        if (zoneAverageAge is null || cityAverageAge is null)
            return null;

        var delta = zoneAverageAge.Value - cityAverageAge.Value;
        if (delta <= -AgeDeltaThresholdYears)
            return new DemographicBadge("Por debajo de la media", DemographicBadge.Info);
        if (delta >= AgeDeltaThresholdYears)
            return new DemographicBadge("Por encima de la media", DemographicBadge.Info);

        return new DemographicBadge("En la media", DemographicBadge.Info);
    }

    public static DemographicBadge? EvaluateAverageHouseholdSize(double? zoneHouseholdSize, double? cityHouseholdSize)
    {
        if (zoneHouseholdSize is null || cityHouseholdSize is null)
            return null;

        var delta = zoneHouseholdSize.Value - cityHouseholdSize.Value;
        if (delta <= -HouseholdSizeDeltaThreshold)
            return new DemographicBadge("Por debajo de la media", DemographicBadge.Info);
        if (delta >= HouseholdSizeDeltaThreshold)
            return new DemographicBadge("Por encima de la media", DemographicBadge.Info);

        return new DemographicBadge("En la media", DemographicBadge.Info);
    }

    public static DemographicBadge? EvaluateSinglePersonHouseholds(double? zonePercentage, double? cityPercentage)
    {
        if (zonePercentage is null || cityPercentage is null)
            return null;

        var delta = zonePercentage.Value - cityPercentage.Value;
        if (delta >= SinglePersonPercentageDeltaThreshold)
            return new DemographicBadge("Alta proporción", DemographicBadge.Warning);
        if (delta <= -SinglePersonPercentageDeltaThreshold)
            return new DemographicBadge("Baja proporción", DemographicBadge.Info);

        return new DemographicBadge("En la media", DemographicBadge.Info);
    }

    public static DemographicBadge? EvaluateChildrenUnder15(double? zonePercentage, double? cityPercentage)
    {
        if (zonePercentage is null || cityPercentage is null)
            return null;

        var delta = zonePercentage.Value - cityPercentage.Value;
        if (delta <= -ChildrenPercentageDeltaThreshold)
            return new DemographicBadge("Pocos", DemographicBadge.Warning);
        if (delta >= ChildrenPercentageDeltaThreshold)
            return new DemographicBadge("Muchos", DemographicBadge.Info);

        return new DemographicBadge("En la media", DemographicBadge.Info);
    }

    public static DemographicBadge? EvaluateSeniorsOver65(double? zonePercentage, double? cityPercentage)
    {
        if (zonePercentage is null || cityPercentage is null)
            return null;

        var delta = zonePercentage.Value - cityPercentage.Value;
        if (delta <= -SeniorsPercentageDeltaThreshold)
            return new DemographicBadge("Zona joven", DemographicBadge.Success);
        if (delta >= SeniorsPercentageDeltaThreshold)
            return new DemographicBadge("Población mayor", DemographicBadge.Warning);

        return new DemographicBadge("En la media", DemographicBadge.Info);
    }
}
