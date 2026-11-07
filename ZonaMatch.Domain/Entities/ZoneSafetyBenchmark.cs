using System.Globalization;

namespace ZonaMatch.Domain.Entities;

// Evaluates safety indicators of a zone against CABA thresholds and reference rates,
// calculating a 1-10 score, qualitative badge, contextual summary, and crime breakdown.
public static class ZoneSafetyBenchmark
{
    public const double DefaultCityAverageRate = 46.9;
    public const int MaxScore = 10;
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-AR");

    public static ZoneSafetyEvaluation? Evaluate(
        double? crimesPerThousandResidents,
        double? cityAverageRate,
        int robberies,
        int thefts,
        int injuries,
        int threats,
        int homicides,
        int roadIncidents)
    {
        if (crimesPerThousandResidents is null)
            return null;

        var effectiveCityRate = cityAverageRate ?? DefaultCityAverageRate;
        var (score, badge) = CalculateScoreAndBadge(crimesPerThousandResidents.Value);
        var breakdown = CalculateBreakdown(robberies, thefts, injuries, threats, homicides, out var totalPenalCrimes);
        var dominantCategoryText = DetermineDominantCategoryText(breakdown, out var dominantCategorySummary);
        var summary = GenerateSummary(score, crimesPerThousandResidents.Value, effectiveCityRate, dominantCategorySummary);

        return new ZoneSafetyEvaluation(
            score,
            MaxScore,
            badge,
            summary,
            crimesPerThousandResidents.Value,
            effectiveCityRate,
            totalPenalCrimes,
            dominantCategoryText,
            breakdown,
            roadIncidents);
    }

    public static (int Score, SafetyBadge Badge) CalculateScoreAndBadge(double crimesPerThousand)
    {
        return crimesPerThousand switch
        {
            <= 30.0 => (10, new SafetyBadge("Muy segura", SafetyBadge.Success)),
            <= 36.0 => (9, new SafetyBadge("Muy segura", SafetyBadge.Success)),
            <= 41.0 => (8, new SafetyBadge("Segura", SafetyBadge.Success)),
            <= 46.9 => (7, new SafetyBadge("Segura", SafetyBadge.Success)),
            <= 52.0 => (6, new SafetyBadge("Moderada", SafetyBadge.Info)),
            <= 58.0 => (5, new SafetyBadge("Moderada", SafetyBadge.Info)),
            <= 68.0 => (4, new SafetyBadge("Por debajo de la media", SafetyBadge.Warning)),
            <= 80.0 => (3, new SafetyBadge("Atención", SafetyBadge.Warning)),
            <= 100.0 => (2, new SafetyBadge("Baja", SafetyBadge.Warning)),
            _ => (1, new SafetyBadge("Crítica", SafetyBadge.Warning))
        };
    }

    public static IReadOnlyList<SafetyBreakdownItem> CalculateBreakdown(
        int robberies,
        int thefts,
        int injuries,
        int threats,
        int homicides,
        out int totalPenalCrimes)
    {
        var total = robberies + thefts + injuries + threats + homicides;
        totalPenalCrimes = total;

        double CalculatePercentage(int count) =>
            total > 0 ? Math.Round(100.0 * count / total, 1) : 0.0;

        return new List<SafetyBreakdownItem>
        {
            new("Robos", robberies, CalculatePercentage(robberies)),
            new("Hurtos", thefts, CalculatePercentage(thefts)),
            new("Lesiones", injuries, CalculatePercentage(injuries)),
            new("Amenazas", threats, CalculatePercentage(threats)),
            new("Homicidios", homicides, CalculatePercentage(homicides))
        };
    }

    public static string DetermineDominantCategoryText(
        IReadOnlyList<SafetyBreakdownItem> breakdown,
        out string dominantCategorySummary)
    {
        var robberiesPct = breakdown.FirstOrDefault(b => b.Category == "Robos")?.Percentage ?? 0.0;
        var theftsPct = breakdown.FirstOrDefault(b => b.Category == "Hurtos")?.Percentage ?? 0.0;
        var propertyPct = Math.Round(robberiesPct + theftsPct, 1);

        if (propertyPct >= 50.0)
        {
            dominantCategorySummary = "robos y hurtos";
            return $"Robos y hurtos concentran el {propertyPct.ToString("0.0", CultureInfo.InvariantCulture)}%";
        }

        var maxCategory = breakdown.OrderByDescending(b => b.Count).FirstOrDefault();
        if (maxCategory is not null && maxCategory.Count > 0)
        {
            dominantCategorySummary = maxCategory.Category.ToLower(SpanishCulture);
            return $"{maxCategory.Category} concentra el {maxCategory.Percentage.ToString("0.0", CultureInfo.InvariantCulture)}%";
        }

        dominantCategorySummary = "delitos contra la propiedad";
        return "Baja incidencia delictiva registrada";
    }

    public static string GenerateSummary(
        int score,
        double rate,
        double cityRate,
        string dominantCategorySummary)
    {
        var rateStr = rate.ToString("0.0", SpanishCulture);
        var cityRateStr = cityRate.ToString("0.0", SpanishCulture);

        return score switch
        {
            >= 9 => $"Registra una actividad delictiva notablemente inferior a la media de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab.), ubicándose entre las zonas más seguras de la ciudad.",
            >= 7 => $"Presenta niveles delictivos por debajo del promedio de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab.), con predominio de {dominantCategorySummary}.",
            >= 5 => $"Presenta niveles delictivos moderados y cercanos al promedio de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab.), con predominio de {dominantCategorySummary}.",
            4 => $"Registra una actividad delictiva superior a la media de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab.), con predominio de {dominantCategorySummary}.",
            3 => $"Registra una tasa delictiva elevada respecto al promedio de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab.), con predominio de {dominantCategorySummary}.",
            _ => $"Registra una concentración crítica de hechos delictivos respecto al promedio de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab.), fuertemente vinculada a áreas de alta circulación de transeúntes y actividad comercial."
        };
    }
}
