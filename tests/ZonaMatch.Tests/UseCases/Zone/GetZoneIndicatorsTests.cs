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
            safety.Score.Should().Be(5.0);
            safety.MaxScore.Should().Be(10);
            safety.Badge.Should().Be(new SafetyBadgeResponse("Moderada", "info"));
            safety.Breakdown.Should().HaveCount(5);
            safety.RoadIncidents.Should().NotBeNull();
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
        public async Task GetZoneIndicators_WhenProvinceLocalityHasDistrictRate_ReturnsDistrictAverage()
        {
            // Arrange
            _repository.Zones.Add(Zone("ramos-mejia", 8, crimeCount: 326, dataSource: ZoneIndicators.SnicSource, jurisdiction: "La Matanza"));
            _repository.DistrictRate = 52.5;

            // Act
            var result = await _useCase.ExecuteAsync("ramos-mejia");

            // Assert
            var safety = result.Safety.Should().BeOfType<ZoneSafetyResponse>().Subject;
            safety.CityAverageRate.Should().Be(52.5);
            safety.Reference.Should().Be(new IndicatorReferenceResponse("La Matanza", 52.5));
            _repository.RequestedDistricts.Should().Equal("La Matanza");
            _repository.DidLoadCityReference.Should().BeFalse();
        }

        [Fact]
        public async Task GetZoneIndicators_WhenLocalitySpansTwoDistricts_AsksForBoth()
        {
            // Arrange
            _repository.Zones.Add(Zone("villa-udaondo", 8, crimeCount: 200, dataSource: ZoneIndicators.SnicSource, jurisdiction: "Merlo, Ituzaingó"));
            _repository.DistrictRate = 39.4;

            // Act
            var result = await _useCase.ExecuteAsync("villa-udaondo");

            // Assert
            _repository.RequestedDistricts.Should().Equal("Merlo", "Ituzaingó");
            result.Safety!.Reference.Should().Be(new IndicatorReferenceResponse("Merlo, Ituzaingó", 39.4));
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsTheDistrict_OmitsSelfComparison()
        {
            // Arrange
            _repository.Zones.Add(Zone("partido-de-la-matanza", 5, crimeCount: 9650, dataSource: ZoneIndicators.SnicSource, jurisdiction: "La Matanza"));
            _repository.DistrictRate = 52.5;

            // Act
            var result = await _useCase.ExecuteAsync("partido-de-la-matanza");

            // Assert
            result.Safety!.CityAverageRate.Should().BeNull();
            result.Safety.Reference.Should().BeNull();
            _repository.RequestedDistricts.Should().BeNull();
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

        [Fact]
        public async Task GetZoneIndicators_WhenZoneHasConditionsData_ReturnsGroupedConditions()
        {
            // Arrange
            var zone = Zone("almagro", 9, crimeCount: 300);
            zone.HouseholdsWithPipedWater = 3900;
            zone.HouseholdsWithSewer = 3800;
            zone.HouseholdsWithGasNetwork = 3600;
            zone.HouseholdsWithGoodHousingQuality = 3400;
            zone.HouseholdsWithUnmetBasicNeeds = 200;
            zone.OvercrowdedHouseholds = 150;
            zone.RentingHouseholds = 1200;
            zone.ResidentsWithEducationData = 8000;
            zone.ResidentsWithSecondaryEducation = 6000;
            zone.ResidentsWithUniversityEducation = 2800;
            zone.ResidentsWithHealthCoverageData = 9000;
            zone.ResidentsWithHealthCoverage = 7200;

            _repository.Zones.Add(zone);

            // Act
            var result = await _useCase.ExecuteAsync("almagro");

            // Assert
            var conditions = result.Conditions;
            conditions.HouseholdsWithUnmetBasicNeedsPercentage.Should().Be(5.0);
            conditions.OvercrowdedHouseholdsPercentage.Should().Be(3.8);
            conditions.RentingHouseholdsPercentage.Should().Be(30.0);
            conditions.HealthCoveragePercentage.Should().Be(80.0);
            conditions.UniversityEducationPercentage.Should().Be(35.0);

            // Grouped: Infrastructure
            conditions.Infrastructure.Should().NotBeNull();
            conditions.Infrastructure.PipedWaterPercentage.Should().Be(97.5);
            conditions.Infrastructure.SewerPercentage.Should().Be(95.0);
            conditions.Infrastructure.GasNetworkPercentage.Should().Be(90.0);
            conditions.Infrastructure.PipedWaterHouseholds.Should().Be(3900);
            conditions.Infrastructure.SewerHouseholds.Should().Be(3800);
            conditions.Infrastructure.GasNetworkHouseholds.Should().Be(3600);

            // Grouped: Habitability
            conditions.Habitability.Should().NotBeNull();
            conditions.Habitability.GoodHousingQualityPercentage.Should().Be(85.0);
            conditions.Habitability.OvercrowdedHouseholdsPercentage.Should().Be(3.8);
            conditions.Habitability.RentingHouseholdsPercentage.Should().Be(30.0);
            conditions.Habitability.UnmetBasicNeedsPercentage.Should().Be(5.0);
            conditions.Habitability.GoodHousingQualityHouseholds.Should().Be(3400);

            // Grouped: Social
            conditions.Social.Should().NotBeNull();
            conditions.Social.HealthCoveragePercentage.Should().Be(80.0);
            conditions.Social.SecondaryEducationPercentage.Should().Be(75.0);
            conditions.Social.UniversityEducationPercentage.Should().Be(35.0);
            conditions.Social.SecondaryEducationResidents.Should().Be(6000);
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

            public double? DistrictRate { get; set; }
            public IReadOnlyCollection<string>? RequestedDistricts { get; private set; }

            public Task<double?> GetDistrictCrimeRateAsync(IReadOnlyCollection<string> districts, CancellationToken cancellationToken = default)
            {
                RequestedDistricts = districts;
                return Task.FromResult(DistrictRate);
            }
        }
    }
}
