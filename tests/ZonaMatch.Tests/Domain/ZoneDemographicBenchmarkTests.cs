using FluentAssertions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain;

public class ZoneDemographicBenchmarkTests
{
    [Fact]
    public void EvaluateAverageAge_WhenBelowCityAverage_ReturnsInfoBadgeWithBelowAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateAverageAge(zoneAverageAge: 40.7, cityAverageAge: 42.5);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Por debajo de la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateAverageAge_WhenAboveCityAverage_ReturnsInfoBadgeWithAboveAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateAverageAge(zoneAverageAge: 44.0, cityAverageAge: 42.5);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Por encima de la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateAverageAge_WhenCloseToCityAverage_ReturnsInfoBadgeWithAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateAverageAge(zoneAverageAge: 42.6, cityAverageAge: 42.5);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("En la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateAverageAge_WhenAnyValueIsNull_ReturnsNull()
    {
        // Arrange & Act
        var badge1 = ZoneDemographicBenchmark.EvaluateAverageAge(zoneAverageAge: null, cityAverageAge: 42.5);
        var badge2 = ZoneDemographicBenchmark.EvaluateAverageAge(zoneAverageAge: 40.0, cityAverageAge: null);

        // Assert
        badge1.Should().BeNull();
        badge2.Should().BeNull();
    }

    [Fact]
    public void EvaluateAverageHouseholdSize_WhenBelowCityAverage_ReturnsInfoBadgeWithBelowAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateAverageHouseholdSize(zoneHouseholdSize: 2.1, cityHouseholdSize: 2.4);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Por debajo de la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateAverageHouseholdSize_WhenAboveCityAverage_ReturnsInfoBadgeWithAboveAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateAverageHouseholdSize(zoneHouseholdSize: 2.7, cityHouseholdSize: 2.4);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Por encima de la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateAverageHouseholdSize_WhenCloseToCityAverage_ReturnsInfoBadgeWithAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateAverageHouseholdSize(zoneHouseholdSize: 2.42, cityHouseholdSize: 2.4);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("En la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateAverageHouseholdSize_WhenAnyValueIsNull_ReturnsNull()
    {
        // Arrange & Act
        var badge1 = ZoneDemographicBenchmark.EvaluateAverageHouseholdSize(zoneHouseholdSize: null, cityHouseholdSize: 2.4);
        var badge2 = ZoneDemographicBenchmark.EvaluateAverageHouseholdSize(zoneHouseholdSize: 2.4, cityHouseholdSize: null);

        // Assert
        badge1.Should().BeNull();
        badge2.Should().BeNull();
    }

    [Fact]
    public void EvaluateSinglePersonHouseholds_WhenSignificantlyAboveCityAverage_ReturnsWarningBadgeWithHighProportionLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateSinglePersonHouseholds(zonePercentage: 40.5, cityPercentage: 32.0);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Alta proporción");
        badge.Variant.Should().Be(DemographicBadge.Warning);
    }

    [Fact]
    public void EvaluateSinglePersonHouseholds_WhenSignificantlyBelowCityAverage_ReturnsInfoBadgeWithLowProportionLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateSinglePersonHouseholds(zonePercentage: 25.0, cityPercentage: 32.0);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Baja proporción");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateSinglePersonHouseholds_WhenCloseToCityAverage_ReturnsInfoBadgeWithAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateSinglePersonHouseholds(zonePercentage: 32.5, cityPercentage: 32.0);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("En la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateSinglePersonHouseholds_WhenAnyValueIsNull_ReturnsNull()
    {
        // Arrange & Act
        var badge1 = ZoneDemographicBenchmark.EvaluateSinglePersonHouseholds(zonePercentage: null, cityPercentage: 32.0);
        var badge2 = ZoneDemographicBenchmark.EvaluateSinglePersonHouseholds(zonePercentage: 32.0, cityPercentage: null);

        // Assert
        badge1.Should().BeNull();
        badge2.Should().BeNull();
    }

    [Fact]
    public void EvaluateChildrenUnder15_WhenSignificantlyBelowCityAverage_ReturnsWarningBadgeWithFewLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateChildrenUnder15(zonePercentage: 9.0, cityPercentage: 13.0);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Pocos");
        badge.Variant.Should().Be(DemographicBadge.Warning);
    }

    [Fact]
    public void EvaluateChildrenUnder15_WhenSignificantlyAboveCityAverage_ReturnsInfoBadgeWithManyLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateChildrenUnder15(zonePercentage: 16.5, cityPercentage: 13.0);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Muchos");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateChildrenUnder15_WhenCloseToCityAverage_ReturnsInfoBadgeWithAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateChildrenUnder15(zonePercentage: 13.2, cityPercentage: 13.0);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("En la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateChildrenUnder15_WhenAnyValueIsNull_ReturnsNull()
    {
        // Arrange & Act
        var badge1 = ZoneDemographicBenchmark.EvaluateChildrenUnder15(zonePercentage: null, cityPercentage: 13.0);
        var badge2 = ZoneDemographicBenchmark.EvaluateChildrenUnder15(zonePercentage: 13.0, cityPercentage: null);

        // Assert
        badge1.Should().BeNull();
        badge2.Should().BeNull();
    }

    [Fact]
    public void EvaluateSeniorsOver65_WhenSignificantlyBelowCityAverage_ReturnsSuccessBadgeWithYoungZoneLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateSeniorsOver65(zonePercentage: 11.8, cityPercentage: 16.5);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Zona joven");
        badge.Variant.Should().Be(DemographicBadge.Success);
    }

    [Fact]
    public void EvaluateSeniorsOver65_WhenSignificantlyAboveCityAverage_ReturnsWarningBadgeWithElderlyPopulationLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateSeniorsOver65(zonePercentage: 20.0, cityPercentage: 16.5);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("Población mayor");
        badge.Variant.Should().Be(DemographicBadge.Warning);
    }

    [Fact]
    public void EvaluateSeniorsOver65_WhenCloseToCityAverage_ReturnsInfoBadgeWithAverageLabel()
    {
        // Arrange & Act
        var badge = ZoneDemographicBenchmark.EvaluateSeniorsOver65(zonePercentage: 16.8, cityPercentage: 16.5);

        // Assert
        badge.Should().NotBeNull();
        badge!.Label.Should().Be("En la media");
        badge.Variant.Should().Be(DemographicBadge.Info);
    }

    [Fact]
    public void EvaluateSeniorsOver65_WhenAnyValueIsNull_ReturnsNull()
    {
        // Arrange & Act
        var badge1 = ZoneDemographicBenchmark.EvaluateSeniorsOver65(zonePercentage: null, cityPercentage: 16.5);
        var badge2 = ZoneDemographicBenchmark.EvaluateSeniorsOver65(zonePercentage: 16.5, cityPercentage: null);

        // Assert
        badge1.Should().BeNull();
        badge2.Should().BeNull();
    }
}
