using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain;

public class ZoneIndicatorsTests
{
    [Theory]
    [InlineData(907, 1000, 90.7)]
    [InlineData(1, 3, 33.3)]
    [InlineData(0, 10, 0.0)]
    public void Percentage_IsRoundedToOneDecimalPlace(int part, int total, double expected)
    {
        Assert.Equal(expected, ZoneIndicators.Percentage(part, total));
    }

    [Fact]
    public void Percentage_IsNullWhenThereIsNoDenominator()
    {
        var zone = new ZoneIndicators { Households = 0, HouseholdsWithInternet = 0 };

        Assert.Null(zone.HouseholdsWithInternetPercentage);
        Assert.Null(zone.ResidentsUnder15Percentage);
    }

    [Fact]
    public void PopulationDensity_IsRoundedResidentsPerSquareKilometer()
    {
        var zone = new ZoneIndicators { Population = 34645, AreaKm2 = 2.566 };

        Assert.Equal(13502, zone.PopulationDensityPerKm2);
    }

    [Fact]
    public void CrimeRate_IsCrimesPerThousandResidents()
    {
        var zone = new ZoneIndicators { Population = 34645, CrimeCount = 1323 };

        Assert.True(zone.HasCrimeData);
        Assert.Equal(38.2, zone.CrimesPerThousandResidents);
    }

    [Fact]
    public void CrimeRate_IsNullWhenCrimeDataIsMissing()
    {
        var zone = new ZoneIndicators { Population = 84410, CrimeCount = null };

        Assert.False(zone.HasCrimeData);
        Assert.Null(zone.CrimesPerThousandResidents);
    }

    [Fact]
    public void AgePercentages_UseResidentsWithReportedAgeAsTheDenominator()
    {
        var zone = new ZoneIndicators
        {
            Population = 1000,
            ResidentsAged0To14 = 100,
            ResidentsAged15To64 = 600,
            ResidentsAged65Plus = 100
        };

        Assert.Equal(12.5, zone.ResidentsUnder15Percentage);
        Assert.Equal(75.0, zone.ResidentsAged15To64Percentage);
        Assert.Equal(12.5, zone.ResidentsAged65PlusPercentage);
    }
}
