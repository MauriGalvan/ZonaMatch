using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zonas;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Zonas
{
    public class ObtenerIndicadoresZonaTests
    {
        private readonly FakeIndicadoresZonaRepository _indicadores = new();
        private readonly ObtenerIndicadoresZona _casoDeUso;

        public ObtenerIndicadoresZonaTests()
        {
            _casoDeUso = new ObtenerIndicadoresZona(_indicadores);
        }

        private static IndicadoresZona Zona(string slug, int nivel, int? delitos,
            string fuente = IndicadoresZona.FuenteMapaDelito, string? ambito = null) => new()
        {
            Slug = slug,
            Nombre = slug,
            Nivel = nivel,
            SuperficieKm2 = 2.5,
            Poblacion = 10000,
            Hogares = 4000,
            HogaresInternet = 3600,
            Delitos = delitos,
            Robos = delitos is null ? null : 100,
            DelitosFuente = delitos is null ? null : fuente,
            DelitosPeriodo = delitos is null ? null : fuente == IndicadoresZona.FuenteSnic ? 2025 : 2024,
            DelitosAmbito = ambito,
        };

        [Fact]
        public async Task Barrio_de_caba_trae_seguridad_comparada_con_la_ciudad()
        {
            _indicadores.Zonas.Add(Zona("villa-luro", 9, delitos: 380));
            _indicadores.Caba = Zona("caba", 4, delitos: 469);

            var resultado = await _casoDeUso.EjecutarAsync("villa-luro");

            Assert.Equal(90.0, resultado.Conectividad.PorcentajeHogaresInternet);
            var seguridad = Assert.IsType<ZonaMatch.Application.DTOs.SeguridadZonaDto>(resultado.Seguridad);
            Assert.Equal(38.0, seguridad.DelitosCada1000Habitantes);
            Assert.Equal(100, seguridad.Robos);
            Assert.Equal(0, seguridad.Homicidios);
            Assert.Equal("CABA", seguridad.Referencia!.Ambito);
            Assert.Equal(46.9, seguridad.Referencia.Valor);
            Assert.Equal(2, resultado.Fuentes.Count);
        }

        [Fact]
        public async Task Execute_WhenZoneIsInProvince_ReturnsPartidoCrimesWithoutCabaReference()
        {
            _indicadores.Zonas.Add(Zona("castelar", 8, delitos: 241, fuente: IndicadoresZona.FuenteSnic, ambito: "Morón"));
            _indicadores.Caba = Zona("caba", 4, delitos: 469);

            var resultado = await _casoDeUso.EjecutarAsync("castelar");

            var seguridad = Assert.IsType<ZonaMatch.Application.DTOs.SeguridadZonaDto>(resultado.Seguridad);
            Assert.Equal("snic", seguridad.Fuente);
            Assert.Equal("Morón", seguridad.Ambito);
            Assert.Equal(2025, seguridad.Anio);
            Assert.Equal(24.1, seguridad.DelitosCada1000Habitantes);
            Assert.Null(seguridad.Referencia);
            Assert.Null(resultado.Conectividad.Referencia);
            Assert.Contains(resultado.Fuentes, f => f.Nivel == "Partido" && f.Anio == 2025);
            Assert.False(_indicadores.PidioCaba);
        }

        [Fact]
        public async Task Zona_sin_delitos_no_trae_seguridad_ni_referencia()
        {
            _indicadores.Zonas.Add(Zona("villa-luzuriaga", 8, delitos: null));
            _indicadores.Caba = Zona("caba", 4, delitos: 469);

            var resultado = await _casoDeUso.EjecutarAsync("villa-luzuriaga");

            Assert.Null(resultado.Seguridad);
            Assert.Null(resultado.Conectividad.Referencia);
            Assert.Single(resultado.Fuentes);
            Assert.False(_indicadores.PidioCaba);
        }

        [Fact]
        public async Task Caba_no_se_compara_consigo_misma()
        {
            var caba = Zona("ciudad-autonoma-de-buenos-aires", 4, delitos: 469);
            _indicadores.Zonas.Add(caba);
            _indicadores.Caba = caba;

            var resultado = await _casoDeUso.EjecutarAsync("ciudad-autonoma-de-buenos-aires");

            Assert.Null(resultado.Seguridad!.Referencia);
            Assert.Null(resultado.Conectividad.Referencia);
        }

        [Fact]
        public async Task Zona_sin_datos_no_encontrada()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _casoDeUso.EjecutarAsync("no-existe"));
        }

        private sealed class FakeIndicadoresZonaRepository : IIndicadoresZonaRepository
        {
            public List<IndicadoresZona> Zonas { get; } = [];
            public IndicadoresZona? Caba { get; set; }
            public bool PidioCaba { get; private set; }

            public Task<IndicadoresZona?> ObtenerPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult(Zonas.FirstOrDefault(z => z.Slug == slug));

            public Task<IndicadoresZona?> ObtenerCabaAsync(CancellationToken cancellationToken = default)
            {
                PidioCaba = true;
                return Task.FromResult(Caba);
            }
        }
    }
}
