using FluentAssertions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain;

public class ZoneIndicatorsTests
{
    [Theory]
    [InlineData(907, 1000, 90.7)]
    [InlineData(1, 3, 33.3)]
    [InlineData(0, 10, 0.0)]
    public void Percentage_WhenDenominatorIsNonZero_RoundsToOneDecimalPlace(int part, int total, double expected)
    {
        ZoneIndicators.Percentage(part, total).Should().Be(expected);
    }

    [Fact]
    public void Percentage_WhenDenominatorIsZero_ReturnsNull()
    {
        var zone = new ZoneIndicators { Households = 0, HouseholdsWithInternet = 0 };

        zone.HouseholdsWithInternetPercentage.Should().BeNull();
        zone.ResidentsUnder15Percentage.Should().BeNull();
    }

    [Fact]
    public void PopulationDensity_WhenAreaIsProvided_RoundsResidentsPerSquareKilometer()
    {
        var zone = new ZoneIndicators { Population = 34645, AreaKm2 = 2.566 };

        zone.PopulationDensityPerKm2.Should().Be(13502);
    }

    [Fact]
    public void CrimeRate_WhenCrimeDataExists_ReturnsCrimesPerThousandResidents()
    {
        var zone = new ZoneIndicators { Population = 34645, CrimeCount = 1323 };

        zone.HasCrimeData.Should().BeTrue();
        zone.CrimesPerThousandResidents.Should().Be(38.2);
    }

    [Fact]
    public void CrimeRate_WhenCrimeDataIsMissing_ReturnsNull()
    {
        var zone = new ZoneIndicators { Population = 84410, CrimeCount = null };

        zone.HasCrimeData.Should().BeFalse();
        zone.CrimesPerThousandResidents.Should().BeNull();
    }

    [Fact]
    public void AgePercentages_WhenPopulationHasReportedAges_UseReportedAgeAsDenominator()
    {
        var zone = new ZoneIndicators
        {
            Population = 1000,
            ResidentsAged0To14 = 100,
            ResidentsAged15To64 = 600,
            ResidentsAged65Plus = 100
        };

        zone.ResidentsUnder15Percentage.Should().Be(12.5);
        zone.ResidentsAged15To64Percentage.Should().Be(75.0);
        zone.ResidentsAged65PlusPercentage.Should().Be(12.5);
    }
}
