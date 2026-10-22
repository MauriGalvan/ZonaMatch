using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zonas;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Zonas
{
    public class ObtenerTransporteZonaTests
    {
        private const string Zona = FakeZonaRepository.Zona;

        private readonly FakeTransporteZonaRepository _transporte = new();
        private readonly ObtenerTransporteZona _casoDeUso;

        public ObtenerTransporteZonaTests()
        {
            _casoDeUso = new ObtenerTransporteZona(_transporte, new FakeZonaRepository());
        }

        private void Salidas(string modo, string linea, string franja, int salidas, int paradas = 3) =>
            _transporte.Salidas.Add(new SalidasLineaZona { Modo = modo, Linea = linea, Franja = franja, Salidas = salidas, Paradas = paradas });

        [Fact]
        public async Task Modos_en_orden_subte_tren_colectivo()
        {
            _transporte.Paradas.AddRange([
                new ParadasModoZona { Modo = "colectivo", Paradas = 90, Lineas = 25 },
                new ParadasModoZona { Modo = "tren", Paradas = 1, Lineas = 1 },
            ]);

            var resultado = await _casoDeUso.EjecutarAsync(Zona);

            Assert.Equal(["tren", "colectivo"], resultado.Modos.Select(m => m.Modo));
            Assert.Equal(2, resultado.Fuentes.Count);
        }

        [Fact]
        public async Task Suma_las_lineas_de_cada_franja_y_ordena_por_hora_pico()
        {
            _transporte.Paradas.Add(new ParadasModoZona { Modo = "colectivo", Paradas = 90, Lineas = 2 });
            Salidas("colectivo", "2", "manana", 30);
            Salidas("colectivo", "47", "manana", 45);
            Salidas("colectivo", "47", "noche", 0);

            var colectivos = Assert.Single((await _casoDeUso.EjecutarAsync(Zona)).Modos);

            var manana = colectivos.Franjas.Single(f => f.Franja == "manana");
            Assert.Equal(2, manana.LineasConServicio);
            Assert.Equal(25.0, manana.SalidasPorHora);
            Assert.Equal(0, colectivos.Franjas.Single(f => f.Franja == "noche").LineasConServicio);
            Assert.Equal(["47", "2"], colectivos.Lineas.Select(l => l.Linea));
            var linea47 = colectivos.Lineas[0].Franjas.Single(f => f.Franja == "manana");
            Assert.Equal(15.0, linea47.SalidasPorHora);
            Assert.Equal(4, linea47.MinutosEntreSalidas);
        }

        [Fact]
        public async Task Zona_sin_transporte_devuelve_vacio()
        {
            var resultado = await _casoDeUso.EjecutarAsync(Zona);

            Assert.Empty(resultado.Modos);
            Assert.Empty(resultado.Fuentes);
        }

        [Fact]
        public async Task Zona_inexistente_no_encontrada()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _casoDeUso.EjecutarAsync("no-existe"));
        }

        private sealed class FakeTransporteZonaRepository : ITransporteZonaRepository
        {
            public List<ParadasModoZona> Paradas { get; } = [];
            public List<SalidasLineaZona> Salidas { get; } = [];

            public Task<IReadOnlyList<ParadasModoZona>> ListarParadasAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ParadasModoZona>>(slug == Zona ? Paradas : []);

            public Task<IReadOnlyList<SalidasLineaZona>> ListarSalidasAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<SalidasLineaZona>>(slug == Zona ? Salidas : []);
        }
    }
}
