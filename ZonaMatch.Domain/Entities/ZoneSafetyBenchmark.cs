using System.Globalization;

namespace ZonaMatch.Domain.Entities;

// Evaluates safety indicators of a zone aligned with the AMBA scoring distribution (ScoringEngine),
// calculating a 1-10 score, qualitative badge, dynamic contextual summary, and crime breakdown.
public static class ZoneSafetyBenchmark
{
    public const double DefaultCityAverageRate = 46.9;
    public const int MaxScore = 10;
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-AR");

    public static ZoneSafetyEvaluation? Evaluate(
        double? crimesPerThousandResidents,
        int? percentile,
        bool isCaba,
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

        var effectivePercentile = percentile ?? EstimatePercentile(crimesPerThousandResidents.Value);
        var (score, badge) = CalculateScoreAndBadge(effectivePercentile);
        var breakdown = CalculateBreakdown(robberies, thefts, injuries, threats, homicides, out var totalPenalCrimes);
        var dominantCategoryText = DetermineDominantCategoryText(breakdown, out var dominantCategorySummary);
        var summary = GenerateSummary(effectivePercentile, crimesPerThousandResidents.Value, isCaba, cityAverageRate, dominantCategorySummary);

        return new ZoneSafetyEvaluation(
            score,
            MaxScore,
            badge,
            summary,
            crimesPerThousandResidents.Value,
            effectivePercentile,
            isCaba ? (cityAverageRate ?? DefaultCityAverageRate) : null,
            totalPenalCrimes,
            dominantCategoryText,
            breakdown,
            roadIncidents);
    }

    public static (double Score, SafetyBadge Badge) CalculateScoreAndBadge(int percentile)
    {
        var score = Math.Round(percentile / 10.0, 1);
        var badge = percentile switch
        {
            >= 80 => new SafetyBadge("Muy segura", SafetyBadge.Success),
            >= 60 => new SafetyBadge("Segura", SafetyBadge.Success),
            >= 40 => new SafetyBadge("Moderada", SafetyBadge.Info),
            >= 20 => new SafetyBadge("Por debajo de la media", SafetyBadge.Warning),
            _ => new SafetyBadge("Atención", SafetyBadge.Warning)
        };

        return (score, badge);
    }

    public static int EstimatePercentile(double crimesPerThousand) => crimesPerThousand switch
    {
        <= 15.0 => 95,
        <= 22.0 => 90,
        <= 28.0 => 80,
        <= 33.0 => 64,
        <= 38.0 => 50,
        <= 44.0 => 35,
        <= 52.0 => 20,
        <= 68.0 => 5,
        _ => 2
    };

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
        int percentile,
        double rate,
        bool isCaba,
        double? cityRate,
        string dominantCategorySummary)
    {
        var rateStr = rate.ToString("0.0", SpanishCulture);

        if (isCaba)
        {
            var effectiveCityRate = cityRate ?? DefaultCityAverageRate;
            var cityRateStr = effectiveCityRate.ToString("0.0", SpanishCulture);

            return percentile switch
            {
                >= 80 => $"Registra una actividad delictiva notablemente baja en CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab., percentil {percentile} del AMBA), ubicándose entre las zonas más seguras de la ciudad.",
                >= 60 => $"Presenta niveles delictivos favorables y por debajo del promedio de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab., percentil {percentile} del AMBA), con predominio de {dominantCategorySummary}.",
                >= 40 => $"Presenta niveles delictivos moderados y cercanos a la media de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab., percentil {percentile} del AMBA), con predominio de {dominantCategorySummary}.",
                >= 20 => $"Registra una actividad delictiva superior a la media de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab., percentil {percentile} del AMBA), con predominio de {dominantCategorySummary}.",
                _ => $"Registra una tasa delictiva elevada respecto al promedio de CABA ({rateStr} vs. {cityRateStr} delitos cada 1.000 hab., percentil {percentile} del AMBA), con predominio de {dominantCategorySummary}."
            };
        }

        return percentile switch
        {
            >= 80 => $"Registra una actividad delictiva notablemente baja dentro del AMBA ({rateStr} delitos estimados cada 1.000 hab., percentil {percentile}), ubicándose entre las zonas más seguras del conurbano.",
            >= 60 => $"Presenta niveles de seguridad favorables dentro del AMBA ({rateStr} delitos estimados cada 1.000 hab., percentil {percentile}), con predominio de {dominantCategorySummary}.",
            >= 40 => $"Presenta niveles delictivos moderados y cercanos a la media del AMBA ({rateStr} delitos estimados cada 1.000 hab., percentil {percentile}), con predominio de {dominantCategorySummary}.",
            >= 20 => $"Registra una actividad delictiva superior a la media del AMBA ({rateStr} delitos estimados cada 1.000 hab., percentil {percentile}), con predominio de {dominantCategorySummary}.",
            _ => $"Registra una tasa delictiva elevada respecto a la media del AMBA ({rateStr} delitos estimados cada 1.000 hab., percentil {percentile}), con predominio de {dominantCategorySummary}."
        };
    }
}
