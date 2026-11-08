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
    public void AverageHouseholdSize_WhenHouseholdsExist_ReturnsPopulationDividedByHouseholds()
    {
        var zone = new ZoneIndicators
        {
            Population = 22470,
            Households = 10273
        };

        zone.AverageHouseholdSize.Should().Be(2.2);
    }

    [Fact]
    public void AverageHouseholdSize_WhenHouseholdsIsZero_ReturnsNull()
    {
        var zone = new ZoneIndicators
        {
            Population = 1000,
            Households = 0
        };

        zone.AverageHouseholdSize.Should().BeNull();
    }

    [Fact]
    public void SinglePersonHouseholdsPercentage_WhenHouseholdsExist_ReturnsPercentage()
    {
        var zone = new ZoneIndicators
        {
            Households = 1000,
            SinglePersonHouseholds = 405
        };

        zone.SinglePersonHouseholdsPercentage.Should().Be(40.5);
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

    [Fact]
    public void DetailedAgeDistribution_WhenResidentsProvided_CalculatesExactPercentages()
    {
        var zone = new ZoneIndicators
        {
            Population = 1000,
            ResidentsAged0To4 = 35,
            ResidentsAged5To14 = 55,
            ResidentsAged15To29 = 188,
            ResidentsAged30To44 = 352,
            ResidentsAged45To64 = 252,
            ResidentsAged65Plus = 118
        };

        zone.ResidentsAged0To4Percentage.Should().Be(3.5);
        zone.ResidentsAged5To14Percentage.Should().Be(5.5);
        zone.ResidentsAged15To29Percentage.Should().Be(18.8);
        zone.ResidentsAged30To44Percentage.Should().Be(35.2);
        zone.ResidentsAged45To64Percentage.Should().Be(25.2);
        zone.ResidentsOver65Percentage.Should().Be(11.8);
        zone.ResidentsUnder15Percentage.Should().Be(9.0);
    }

    [Fact]
    public void LivingConditionsPercentages_WhenDataProvided_CalculatesExactPercentages()
    {
        // Arrange
        var zone = new ZoneIndicators
        {
            Households = 2000,
            HouseholdsWithPipedWater = 1980,
            HouseholdsWithSewer = 1960,
            HouseholdsWithGasNetwork = 1800,
            HouseholdsWithGoodHousingQuality = 1700,
            ResidentsWithEducationData = 5000,
            ResidentsWithSecondaryEducation = 3500,
            ResidentsWithUniversityEducation = 1200
        };

        // Assert
        zone.HouseholdsWithPipedWaterPercentage.Should().Be(99.0);
        zone.HouseholdsWithSewerPercentage.Should().Be(98.0);
        zone.HouseholdsWithGasNetworkPercentage.Should().Be(90.0);
        zone.HouseholdsWithGoodHousingQualityPercentage.Should().Be(85.0);
        zone.SecondaryEducationPercentage.Should().Be(70.0);
        zone.UniversityEducationPercentage.Should().Be(24.0);
    }
}
