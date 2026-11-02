using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zones;
using ZonaMatch.Application.DTOs.ZoneIndicators.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Zones
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

            Assert.Equal(90.0, result.Conectividad.PorcentajeHogaresInternet);
            var safety = Assert.IsType<ZoneSafetyResponse>(result.Seguridad);
            Assert.Equal(38.0, safety.DelitosCada1000Habitantes);
            Assert.Equal(100, safety.Robos);
            Assert.Equal(0, safety.Homicidios);
            Assert.Equal("CABA", safety.Referencia!.Ambito);
            Assert.Equal(46.9, safety.Referencia.Valor);
            Assert.Equal(2, result.Fuentes.Count);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsInProvince_ReturnsDistrictCrimesWithoutCityReference()
        {
            _repository.Zones.Add(Zone("castelar", 8, crimeCount: 241, dataSource: ZoneIndicators.FuenteSnic, jurisdiction: "Morón"));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            var result = await _useCase.ExecuteAsync("castelar");

            var safety = Assert.IsType<ZoneSafetyResponse>(result.Seguridad);
            Assert.Equal("snic", safety.Fuente);
            Assert.Equal("Morón", safety.Ambito);
            Assert.Equal(2025, safety.Anio);
            Assert.Equal(24.1, safety.DelitosCada1000Habitantes);
            Assert.Null(safety.Referencia);
            Assert.Null(result.Conectividad.Referencia);
            Assert.Contains(result.Fuentes, source => source.Nivel == "Partido" && source.Anio == 2025);
            Assert.False(_repository.DidLoadCityReference);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneHasNoCrimes_OmitsSafetyAndReference()
        {
            _repository.Zones.Add(Zone("villa-luzuriaga", 8, crimeCount: null));
            _repository.CityReference = Zone("caba", 4, crimeCount: 469);

            var result = await _useCase.ExecuteAsync("villa-luzuriaga");

            Assert.Null(result.Seguridad);
            Assert.Null(result.Conectividad.Referencia);
            Assert.Single(result.Fuentes);
            Assert.False(_repository.DidLoadCityReference);
        }

        [Fact]
        public async Task GetZoneIndicators_WhenZoneIsBuenosAiresCity_OmitsSelfComparison()
        {
            var buenosAiresCity = Zone("ciudad-autonoma-de-buenos-aires", 4, crimeCount: 469);
            _repository.Zones.Add(buenosAiresCity);
            _repository.CityReference = buenosAiresCity;

            var result = await _useCase.ExecuteAsync("ciudad-autonoma-de-buenos-aires");

            Assert.Null(result.Seguridad!.Referencia);
            Assert.Null(result.Conectividad.Referencia);
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
            string dataSource = ZoneIndicators.FuenteMapaDelito,
            string? jurisdiction = null) => new()
        {
            Slug = slug,
            Nombre = slug,
            Nivel = administrativeLevel,
            SuperficieKm2 = 2.5,
            Poblacion = 10000,
            Hogares = 4000,
            HogaresInternet = 3600,
            Delitos = crimeCount,
            Robos = crimeCount is null ? null : 100,
            DelitosFuente = crimeCount is null ? null : dataSource,
            DelitosPeriodo = crimeCount is null ? null : dataSource == ZoneIndicators.FuenteSnic ? 2025 : 2024,
            DelitosAmbito = jurisdiction,
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
