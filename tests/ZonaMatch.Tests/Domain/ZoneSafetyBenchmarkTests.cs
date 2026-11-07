using FluentAssertions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain;

public class ZoneSafetyBenchmarkTests
{
    [Theory]
    [InlineData(25.6, 10, "Muy segura", SafetyBadge.Success)]
    [InlineData(30.0, 10, "Muy segura", SafetyBadge.Success)]
    [InlineData(30.1, 9, "Muy segura", SafetyBadge.Success)]
    [InlineData(36.0, 9, "Muy segura", SafetyBadge.Success)]
    [InlineData(36.1, 8, "Segura", SafetyBadge.Success)]
    [InlineData(40.7, 8, "Segura", SafetyBadge.Success)]
    [InlineData(41.0, 8, "Segura", SafetyBadge.Success)]
    [InlineData(41.1, 7, "Segura", SafetyBadge.Success)]
    [InlineData(46.9, 7, "Segura", SafetyBadge.Success)]
    [InlineData(47.0, 6, "Moderada", SafetyBadge.Info)]
    [InlineData(52.0, 6, "Moderada", SafetyBadge.Info)]
    [InlineData(52.1, 5, "Moderada", SafetyBadge.Info)]
    [InlineData(58.0, 5, "Moderada", SafetyBadge.Info)]
    [InlineData(58.1, 4, "Por debajo de la media", SafetyBadge.Warning)]
    [InlineData(65.5, 4, "Por debajo de la media", SafetyBadge.Warning)]
    [InlineData(68.0, 4, "Por debajo de la media", SafetyBadge.Warning)]
    [InlineData(68.1, 3, "Atención", SafetyBadge.Warning)]
    [InlineData(80.0, 3, "Atención", SafetyBadge.Warning)]
    [InlineData(80.1, 2, "Baja", SafetyBadge.Warning)]
    [InlineData(100.0, 2, "Baja", SafetyBadge.Warning)]
    [InlineData(100.1, 1, "Crítica", SafetyBadge.Warning)]
    [InlineData(160.0, 1, "Crítica", SafetyBadge.Warning)]
    public void CalculateScoreAndBadge_WhenRateIsGiven_ReturnsExpectedScoreAndBadge(
        double rate,
        int expectedScore,
        string expectedLabel,
        string expectedVariant)
    {
        // Act
        var (score, badge) = ZoneSafetyBenchmark.CalculateScoreAndBadge(rate);

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
            cityAverageRate: 46.9,
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
    public void Evaluate_WhenValidDataProvided_CalculatesCompleteEvaluation()
    {
        // Arrange (Chacarita sample metrics)
        const double rate = 65.5;
        const double cityRate = 46.9;
        const int robberies = 1007;
        const int thefts = 847;
        const int injuries = 107;
        const int threats = 89;
        const int homicides = 0;
        const int roadIncidents = 181;

        // Act
        var evaluation = ZoneSafetyBenchmark.Evaluate(
            rate,
            cityRate,
            robberies,
            thefts,
            injuries,
            threats,
            homicides,
            roadIncidents);

        // Assert
        evaluation.Should().NotBeNull();
        evaluation!.Score.Should().Be(4);
        evaluation.MaxScore.Should().Be(10);
        evaluation.Badge.Label.Should().Be("Por debajo de la media");
        evaluation.Badge.Variant.Should().Be(SafetyBadge.Warning);
        evaluation.CrimesPerThousandResidents.Should().Be(65.5);
        evaluation.CityAverageRate.Should().Be(46.9);
        evaluation.TotalCrimes.Should().Be(2050);
        evaluation.RoadIncidents.Should().Be(181);
        evaluation.DominantCategoryText.Should().Be("Robos y hurtos concentran el 90.4%");
        evaluation.Summary.Should().Contain("superior a la media de CABA (65,5 vs. 46,9 delitos cada 1.000 hab.)");

        evaluation.Breakdown.Should().HaveCount(5);
        var robos = evaluation.Breakdown.First(b => b.Category == "Robos");
        robos.Count.Should().Be(1007);
        robos.Percentage.Should().Be(49.1);

        var hurtos = evaluation.Breakdown.First(b => b.Category == "Hurtos");
        hurtos.Count.Should().Be(847);
        hurtos.Percentage.Should().Be(41.3);

        var lesiones = evaluation.Breakdown.First(b => b.Category == "Lesiones");
        lesiones.Count.Should().Be(107);
        lesiones.Percentage.Should().Be(5.2);

        var amenazas = evaluation.Breakdown.First(b => b.Category == "Amenazas");
        amenazas.Count.Should().Be(89);
        amenazas.Percentage.Should().Be(4.3);

        var homi = evaluation.Breakdown.First(b => b.Category == "Homicidios");
        homi.Count.Should().Be(0);
        homi.Percentage.Should().Be(0.0);
    }

    [Fact]
    public void Evaluate_WhenCityAverageRateIsNull_UsesDefaultCityAverageRate()
    {
        // Act
        var evaluation = ZoneSafetyBenchmark.Evaluate(
            crimesPerThousandResidents: 25.6,
            cityAverageRate: null,
            robberies: 10,
            thefts: 10,
            injuries: 2,
            threats: 1,
            homicides: 0,
            roadIncidents: 5);

        // Assert
        evaluation.Should().NotBeNull();
        evaluation!.CityAverageRate.Should().Be(ZoneSafetyBenchmark.DefaultCityAverageRate);
        evaluation.Score.Should().Be(10);
        evaluation.Badge.Label.Should().Be("Muy segura");
        evaluation.Badge.Variant.Should().Be(SafetyBadge.Success);
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
