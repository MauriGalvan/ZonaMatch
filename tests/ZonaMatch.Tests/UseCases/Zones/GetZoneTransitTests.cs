using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zones;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases;

namespace ZonaMatch.Tests.UseCases.Zones
{
    public class GetZoneTransitTests
    {
        private const string ZoneSlug = FakeZonaRepository.Zona;

        private readonly FakeTransitZoneRepository _transitRepository = new();
        private readonly GetZoneTransit _useCase;

        public GetZoneTransitTests()
        {
            _useCase = new GetZoneTransit(_transitRepository, new FakeZonaRepository());
        }

        private void AddDepartures(string mode, string line, string timeWindow, int departures, int stops = 3) =>
            _transitRepository.Departures.Add(new SalidasLineaZona
                { Modo = mode, Linea = line, Franja = timeWindow, Salidas = departures, Paradas = stops });

        [Fact]
        public async Task GetZoneTransit_ReturnsModesInDisplayOrder()
        {
            _transitRepository.Stops.AddRange([
                new ParadasModoZona { Modo = "colectivo", Paradas = 90, Lineas = 25 },
                new ParadasModoZona { Modo = "tren", Paradas = 1, Lineas = 1 },
            ]);

            var result = await _useCase.ExecuteAsync(ZoneSlug);

            Assert.Equal(["tren", "colectivo"], result.Modos.Select(mode => mode.Modo));
            Assert.Equal(2, result.Fuentes.Count);
        }

        [Fact]
        public async Task GetZoneTransit_SumsEachTimeWindowAndSortsLinesByPeakDepartures()
        {
            _transitRepository.Stops.Add(new ParadasModoZona { Modo = "colectivo", Paradas = 90, Lineas = 2 });
            AddDepartures("colectivo", "2", "manana", 30);
            AddDepartures("colectivo", "47", "manana", 45);
            AddDepartures("colectivo", "47", "noche", 0);

            var buses = Assert.Single((await _useCase.ExecuteAsync(ZoneSlug)).Modos);

            var morning = buses.Franjas.Single(window => window.Franja == "manana");
            Assert.Equal(2, morning.LineasConServicio);
            Assert.Equal(25.0, morning.SalidasPorHora);
            Assert.Equal(0, buses.Franjas.Single(window => window.Franja == "noche").LineasConServicio);
            Assert.Equal(["47", "2"], buses.Lineas.Select(line => line.Linea));
            var line47Morning = buses.Lineas[0].Franjas.Single(window => window.Franja == "manana");
            Assert.Equal(15.0, line47Morning.SalidasPorHora);
            Assert.Equal(4, line47Morning.MinutosEntreSalidas);
        }

        [Fact]
        public async Task GetZoneTransit_WhenZoneHasNoStops_ReturnsEmptyTransitData()
        {
            var result = await _useCase.ExecuteAsync(ZoneSlug);

            Assert.Empty(result.Modos);
            Assert.Empty(result.Fuentes);
        }

        [Fact]
        public async Task GetZoneTransit_WhenZoneDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe"));
        }

        private sealed class FakeTransitZoneRepository : ITransporteZonaRepository
        {
            public List<ParadasModoZona> Stops { get; } = [];
            public List<SalidasLineaZona> Departures { get; } = [];

            public Task<IReadOnlyList<ParadasModoZona>> ListarParadasAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ParadasModoZona>>(slug == ZoneSlug ? Stops : []);

            public Task<IReadOnlyList<SalidasLineaZona>> ListarSalidasAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<SalidasLineaZona>>(slug == ZoneSlug ? Departures : []);
        }
    }
}
