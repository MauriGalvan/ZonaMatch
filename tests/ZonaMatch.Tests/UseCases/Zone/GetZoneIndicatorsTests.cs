using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Application.DTOs.ZoneIndicator.Responses;
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
            _repository.Zones.Add(Zone("villa-luro", 9, crimeCount: 380));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            var result = await _useCase.ExecuteAsync("villa-luro");

            Assert.Equal(90.0, result.Connectivity.HouseholdsWithInternetPercentage);
            var safety = Assert.IsType<ZoneSafetyResponse>(result.Safety);
            Assert.Equal(38.0, safety.CrimesPer1000Residents);
            Assert.Equal(100, safety.Robberies);
            Assert.Equal(0, safety.Homicides);
            Assert.Equal("CABA", safety.Reference!.Scope);
            Assert.Equal(46.9, safety.Reference.Value);
            Assert.Equal(2, result.Sources.Count);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsInProvince_ReturnsDistrictCrimesWithoutCityReference()
        {
            _repository.Zones.Add(Zone("castelar", 8, crimeCount: 241, dataSource: ZoneIndicators.SnicSource, jurisdiction: "Morón"));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            var result = await _useCase.ExecuteAsync("castelar");

            var safety = Assert.IsType<ZoneSafetyResponse>(result.Safety);
            Assert.Equal("snic", safety.Source);
            Assert.Equal("Morón", safety.Scope);
            Assert.Equal(2025, safety.Year);
            Assert.Equal(24.1, safety.CrimesPer1000Residents);
            Assert.Null(safety.Reference);
            Assert.Null(result.Connectivity.Reference);
            Assert.Contains(result.Sources, source => source.Level == "Partido" && source.Year == 2025);
            Assert.False(_repository.DidLoadCityReference);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneHasNoCrimes_OmitsSafetyAndReference()
        {
            _repository.Zones.Add(Zone("villa-luzuriaga", 8, crimeCount: null));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            var result = await _useCase.ExecuteAsync("villa-luzuriaga");

            Assert.Null(result.Safety);
            Assert.Null(result.Connectivity.Reference);
            Assert.Single(result.Sources);
            Assert.False(_repository.DidLoadCityReference);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsBuenosAiresCity_OmitsSelfComparison()
        {
            var buenosAiresCity = Zone("ciudad-autonoma-de-buenos-aires", 4, crimeCount: 469);
            _repository.Zones.Add(buenosAiresCity);
            _repository.CityReference = buenosAiresCity;

            var result = await _useCase.ExecuteAsync("ciudad-autonoma-de-buenos-aires");

            Assert.Null(result.Safety!.Reference);
            Assert.Null(result.Connectivity.Reference);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneHasNoData_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe"));
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
