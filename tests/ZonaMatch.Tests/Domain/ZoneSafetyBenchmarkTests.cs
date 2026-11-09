using FluentAssertions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain;

public class ZoneSafetyBenchmarkTests
{
    [Theory]
    [InlineData(95, 9.5, "Muy segura", SafetyBadge.Success)]
    [InlineData(80, 8.0, "Muy segura", SafetyBadge.Success)]
    [InlineData(79, 7.9, "Segura", SafetyBadge.Success)]
    [InlineData(64, 6.4, "Segura", SafetyBadge.Success)]
    [InlineData(60, 6.0, "Segura", SafetyBadge.Success)]
    [InlineData(59, 5.9, "Moderada", SafetyBadge.Info)]
    [InlineData(50, 5.0, "Moderada", SafetyBadge.Info)]
    [InlineData(40, 4.0, "Moderada", SafetyBadge.Info)]
    [InlineData(39, 3.9, "Por debajo de la media", SafetyBadge.Warning)]
    [InlineData(25, 2.5, "Por debajo de la media", SafetyBadge.Warning)]
    [InlineData(20, 2.0, "Por debajo de la media", SafetyBadge.Warning)]
    [InlineData(19, 1.9, "Atención", SafetyBadge.Warning)]
    [InlineData(5, 0.5, "Atención", SafetyBadge.Warning)]
    [InlineData(0, 0.0, "Atención", SafetyBadge.Warning)]
    public void CalculateScoreAndBadge_WhenPercentileIsGiven_ReturnsExpectedScoreAndBadge(
        int percentile,
        double expectedScore,
        string expectedLabel,
        string expectedVariant)
    {
        // Act
        var (score, badge) = ZoneSafetyBenchmark.CalculateScoreAndBadge(percentile);

        // Assert
        score.Should().Be(expectedScore);
        badge.Label.Should().Be(expectedLabel);
        badge.Variant.Should().Be(expectedVariant);
    }

    [Fact]
    public void Evaluate_WhenRateIsNull_ReturnsNull()
    {
        // Act
        var result = ZoneSafetyBenchmark.Evaluate(
            crimesPerThousandResidents: null,
            percentile: 64,
            isCaba: false,
            cityAverageRate: null,
            robberies: 100,
            thefts: 50,
            injuries: 10,
            threats: 5,
            homicides: 0,
            roadIncidents: 20);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WhenConurbanoZoneEvaluated_UsesAmbaContextAndExcludesCityComparison()
    {
        // Arrange (Ramos Mejía sample metrics)
        const double rate = 32.6;
        const int percentile = 64;
        const bool isCaba = false;
        const int robberies = 1311;
        const int thefts = 581;
        const int injuries = 312;
        const int threats = 201;
        const int homicides = 115;
        const int roadIncidents = 176;

        // Act
        var evaluation = ZoneSafetyBenchmark.Evaluate(
            rate,
            percentile,
            isCaba,
            cityAverageRate: null,
            robberies,
            thefts,
            injuries,
            threats,
            homicides,
            roadIncidents);

        // Assert
        evaluation.Should().NotBeNull();
        evaluation!.Score.Should().Be(6.4);
        evaluation.MaxScore.Should().Be(10);
        evaluation.Percentile.Should().Be(64);
        evaluation.Badge.Label.Should().Be("Segura");
        evaluation.Badge.Variant.Should().Be(SafetyBadge.Success);
        evaluation.CrimesPerThousandResidents.Should().Be(32.6);
        evaluation.CityAverageRate.Should().BeNull();
        evaluation.TotalCrimes.Should().Be(2520);
        evaluation.RoadIncidents.Should().Be(176);
        evaluation.DominantCategoryText.Should().Be("Robos y hurtos concentran el 75.1%");
        evaluation.Summary.Should().Contain("dentro del AMBA (32,6 delitos estimados cada 1.000 hab., percentil 64)");
        evaluation.Summary.Should().NotContain("de la ciudad");
    }

    [Fact]
    public void Evaluate_WhenCabaZoneEvaluated_UsesCabaContextAndIncludesCityComparison()
    {
        // Arrange (Parque Patricios sample metrics)
        const double rate = 66.5;
        const int percentile = 5;
        const bool isCaba = true;
        const double cityRate = 46.9;
        const int robberies = 1200;
        const int thefts = 900;
        const int injuries = 125;
        const int threats = 90;
        const int homicides = 10;
        const int roadIncidents = 150;

        // Act
        var evaluation = ZoneSafetyBenchmark.Evaluate(
            rate,
            percentile,
            isCaba,
            cityRate,
            robberies,
            thefts,
            injuries,
            threats,
            homicides,
            roadIncidents);

        // Assert
        evaluation.Should().NotBeNull();
        evaluation!.Score.Should().Be(0.5);
        evaluation.MaxScore.Should().Be(10);
        evaluation.Percentile.Should().Be(5);
        evaluation.Badge.Label.Should().Be("Atención");
        evaluation.Badge.Variant.Should().Be(SafetyBadge.Warning);
        evaluation.CityAverageRate.Should().Be(46.9);
        evaluation.Summary.Should().Contain("promedio de CABA (66,5 vs. 46,9 delitos cada 1.000 hab., percentil 5 del AMBA)");
    }

    [Fact]
    public void EstimatePercentile_WhenValidRatesGiven_ReturnsExpectedEstimates()
    {
        ZoneSafetyBenchmark.EstimatePercentile(12.0).Should().Be(95);
        ZoneSafetyBenchmark.EstimatePercentile(25.0).Should().Be(80);
        ZoneSafetyBenchmark.EstimatePercentile(32.6).Should().Be(64);
        ZoneSafetyBenchmark.EstimatePercentile(50.0).Should().Be(20);
        ZoneSafetyBenchmark.EstimatePercentile(66.5).Should().Be(5);
    }

    [Fact]
    public void CalculateBreakdown_WhenTotalCrimesIsZero_ReturnsZeroPercentages()
    {
        // Act
        var breakdown = ZoneSafetyBenchmark.CalculateBreakdown(0, 0, 0, 0, 0, out var total);

        // Assert
        total.Should().Be(0);
        breakdown.Should().HaveCount(5);
        breakdown.Should().OnlyContain(item => item.Percentage == 0.0 && item.Count == 0);
    }

    [Fact]
    public void DetermineDominantCategoryText_WhenAllZero_ReturnsLowIncidenceMessage()
    {
        // Arrange
        var breakdown = ZoneSafetyBenchmark.CalculateBreakdown(0, 0, 0, 0, 0, out _);

        // Act
        var text = ZoneSafetyBenchmark.DetermineDominantCategoryText(breakdown, out var summary);

        // Assert
        text.Should().Be("Baja incidencia delictiva registrada");
        summary.Should().Be("delitos contra la propiedad");
    }
}
