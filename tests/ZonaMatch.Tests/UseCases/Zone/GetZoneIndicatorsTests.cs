using FluentAssertions;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.DTOs.ZoneIndicator.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Zone
{
    public class GetZoneIndicatorsTests
    {
        private readonly FakeZoneIndicatorsRepository _repository = new();
        private readonly GetZoneIndicators _useCase;

        public GetZoneIndicatorsTests()
        {
            _useCase = new GetZoneIndicators(_repository);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsInBuenosAiresCity_ReturnsCityCrimeComparison()
        {
            // Arrange
            _repository.Zones.Add(Zone("villa-luro", 9, crimeCount: 380));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            // Act
            var result = await _useCase.ExecuteAsync("villa-luro");

            // Assert
            result.Connectivity.HouseholdsWithInternetPercentage.Should().Be(90.0);
            var safety = result.Safety.Should().BeOfType<ZoneSafetyResponse>().Subject;
            safety.CrimesPer1000Residents.Should().Be(38.0);
            safety.Robberies.Should().Be(100);
            safety.Homicides.Should().Be(0);
            safety.Reference.Should().NotBeNull();
            safety.Reference!.Scope.Should().Be("CABA");
            safety.Reference.Value.Should().Be(46.9);
            result.Sources.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsInBuenosAiresCity_ReturnsPopulationWithDemographicBadgesAndDistribution()
        {
            // Arrange
            var zone = new ZoneIndicators
            {
                Slug = "palermo-soho",
                Name = "Palermo Soho",
                Level = 9,
                AreaKm2 = 2.0,
                Population = 22470,
                Households = 10273,
                Dwellings = 12010,
                AverageAge = 40.7,
                ResidentsAged0To4 = 786,
                ResidentsAged5To14 = 1236,
                ResidentsAged15To29 = 4224,
                ResidentsAged30To44 = 7909,
                ResidentsAged45To64 = 5662,
                ResidentsAged65Plus = 2653,
                SinglePersonHouseholds = 4161,
                CrimeCount = 380,
                CrimeDataSource = ZoneIndicators.CrimeMapSource,
                CrimeDataYear = 2024
            };

            var city = new ZoneIndicators
            {
                Slug = "caba",
                Name = "CABA",
                Level = 4,
                AreaKm2 = 200.0,
                Population = 3000000,
                Households = 1250000,
                AverageAge = 42.5,
                ResidentsAged0To14 = 390000,
                ResidentsAged15To64 = 2115000,
                ResidentsAged65Plus = 495000,
                SinglePersonHouseholds = 400000,
                CrimeCount = 469,
                CrimeDataSource = ZoneIndicators.CrimeMapSource,
                CrimeDataYear = 2024
            };

            _repository.Zones.Add(zone);
            _repository.CityReference = city;

            // Act
            var result = await _useCase.ExecuteAsync("palermo-soho");

            // Assert
            var pop = result.Population;
            pop.Total.Should().Be(22470);
            pop.Households.Should().Be(10273);
            pop.Dwellings.Should().Be(12010);
            pop.AverageAge.Should().Be(40.7);
            pop.AverageAgeBadge.Should().Be(new DemographicBadgeResponse("Por debajo de la media", "info"));
            pop.AverageHouseholdSize.Should().Be(2.2);
            pop.AverageHouseholdSizeBadge.Should().Be(new DemographicBadgeResponse("Por debajo de la media", "info"));
            pop.SinglePersonHouseholdsPercentage.Should().Be(40.5);
            pop.SinglePersonHouseholdsBadge.Should().Be(new DemographicBadgeResponse("Alta proporción", "warning"));
            pop.ChildrenUnder15Badge.Should().Be(new DemographicBadgeResponse("Pocos", "warning"));
            pop.SeniorsOver65Badge.Should().Be(new DemographicBadgeResponse("Zona joven", "success"));

            pop.AgeDistribution.Age0To4Percentage.Should().Be(3.5);
            pop.AgeDistribution.Age5To14Percentage.Should().Be(5.5);
            pop.AgeDistribution.Age15To29Percentage.Should().Be(18.8);
            pop.AgeDistribution.Age30To44Percentage.Should().Be(35.2);
            pop.AgeDistribution.Age45To64Percentage.Should().Be(25.2);
            pop.AgeDistribution.Over65Percentage.Should().Be(11.8);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenCityReferenceIsMissing_ReturnsCrimeDataWithoutComparisons()
        {
            // Arrange
            _repository.Zones.Add(Zone("caballito", 4, crimeCount: 380));

            // Act
            var result = await _useCase.ExecuteAsync("caballito");

            // Assert
            var safety = result.Safety.Should().BeOfType<ZoneSafetyResponse>().Subject;
            safety.Reference.Should().BeNull();
            result.Connectivity.Reference.Should().BeNull();
            result.Population.AverageAgeBadge.Should().BeNull();
            result.Population.AverageHouseholdSizeBadge.Should().BeNull();
            result.Population.SinglePersonHouseholdsBadge.Should().BeNull();
            _repository.DidLoadCityReference.Should().BeTrue();
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsInProvince_ReturnsDistrictCrimesWithoutCityReference()
        {
            // Arrange
            _repository.Zones.Add(Zone("castelar", 8, crimeCount: 241, dataSource: ZoneIndicators.SnicSource, jurisdiction: "Morón"));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            // Act
            var result = await _useCase.ExecuteAsync("castelar");

            // Assert
            var safety = result.Safety.Should().BeOfType<ZoneSafetyResponse>().Subject;
            safety.Source.Should().Be("snic");
            safety.Scope.Should().Be("Morón");
            safety.Year.Should().Be(2025);
            safety.CrimesPer1000Residents.Should().Be(24.1);
            safety.Reference.Should().BeNull();
            result.Connectivity.Reference.Should().BeNull();
            result.Population.AverageAgeBadge.Should().BeNull();
            result.Sources.Should().Contain(source => source.Level == "Partido" && source.Year == 2025);
            _repository.DidLoadCityReference.Should().BeFalse();
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneHasNoCrimes_OmitsSafetyAndReference()
        {
            // Arrange
            _repository.Zones.Add(Zone("villa-luzuriaga", 8, crimeCount: null));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            // Act
            var result = await _useCase.ExecuteAsync("villa-luzuriaga");

            // Assert
            result.Safety.Should().BeNull();
            result.Connectivity.Reference.Should().BeNull();
            result.Population.AverageAgeBadge.Should().BeNull();
            result.Sources.Should().ContainSingle();
            _repository.DidLoadCityReference.Should().BeFalse();
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsBuenosAiresCity_OmitsSelfComparison()
        {
            // Arrange
            var buenosAiresCity = Zone("ciudad-autonoma-de-buenos-aires", 4, crimeCount: 469);
            _repository.Zones.Add(buenosAiresCity);
            _repository.CityReference = buenosAiresCity;

            // Act
            var result = await _useCase.ExecuteAsync("ciudad-autonoma-de-buenos-aires");

            // Assert
            result.Safety!.Reference.Should().BeNull();
            result.Connectivity.Reference.Should().BeNull();
            result.Population.AverageAgeBadge.Should().BeNull();
            result.Population.AverageHouseholdSizeBadge.Should().BeNull();
            result.Population.SinglePersonHouseholdsBadge.Should().BeNull();
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneHasNoData_ThrowsNotFoundException()
        {
            // Arrange & Act
            var act = () => _useCase.ExecuteAsync("no-existe");

            // Assert
            await act.Should().ThrowAsync<NotFoundException>();
        }

        private static ZoneIndicators Zone(
            string slug,
            int administrativeLevel,
            int? crimeCount,
            string dataSource = ZoneIndicators.CrimeMapSource,
            string? jurisdiction = null) => new()
        {
            Slug = slug,
            Name = slug,
            Level = administrativeLevel,
            AreaKm2 = 2.5,
            Population = 10000,
            Households = 4000,
            HouseholdsWithInternet = 3600,
            AverageAge = 40.0,
            CrimeCount = crimeCount,
            Robberies = crimeCount is null ? null : 100,
            CrimeDataSource = crimeCount is null ? null : dataSource,
            CrimeDataYear = crimeCount is null ? null : dataSource == ZoneIndicators.SnicSource ? 2025 : 2024,
            CrimeDataCoverage = jurisdiction,
        };

        private sealed class FakeZoneIndicatorsRepository : IZoneIndicatorsRepository
        {
            public List<ZoneIndicators> Zones { get; } = [];
            public ZoneIndicators? CityReference { get; set; }
            public bool DidLoadCityReference { get; private set; }

            public Task<ZoneIndicators?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult(Zones.FirstOrDefault(zone => zone.Slug == slug));

            public Task<ZoneIndicators?> GetCapitalCityAsync(CancellationToken cancellationToken = default)
            {
                DidLoadCityReference = true;
                return Task.FromResult(CityReference);
            }
        }
    }
}
