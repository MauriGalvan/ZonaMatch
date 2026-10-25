using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Puntajes;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.UseCases.Puntajes
{
    public class CalcularPuntajesTests
    {
        private readonly FakePerfilesZonaRepository _perfiles = new();
        private readonly FakeRuteoClient _ruteo = new();
        private readonly FakeZonaRepository _zonas = new();
        private readonly CalcularPuntajes _casoDeUso;

        public CalcularPuntajesTests()
        {
            _casoDeUso = new CalcularPuntajes(_perfiles, _ruteo, _zonas);
        }

        private void Zona(string slug, int delitos) => _perfiles.Zonas.Add(new PerfilZona
        {
            Slug = slug,
            Nombre = slug,
            Nivel = 9,
            Poblacion = 1000,
            SuperficieKm2 = 1,
            Delitos = delitos,
            Latitud = -34.6,
            Longitud = -58.5,
            AlquilerPromedio = 600_000,
        });

        private static CalcularPuntajesRequest Pedido() => new()
        {
            Criterios = new() { ["seguridad"] = "alta", ["inventado"] = "alta", ["salud"] = "urgente" },
        };

        [Fact]
        public async Task Rechaza_zonas_agregadas_inexistentes()
        {
            Zona("villa-luro", 30);

            var pedido = Pedido();
            pedido.ZonasAgregadas = ["no-existe"];

            await Assert.ThrowsAsync<NotFoundException>(() => _casoDeUso.EjecutarAsync(pedido));
        }

        [Fact]
        public async Task Ignora_criterios_y_prioridades_desconocidos()
        {
            Zona("villa-luro", 30);

            var zona = Assert.Single((await _casoDeUso.EjecutarAsync(Pedido())).Zonas);

            Assert.Equal(["seguridad"], zona.Componentes.Select(c => c.Criterio));
        }

        [Fact]
        public async Task El_top_automatico_incluye_la_zona_inicial_y_recomienda_las_siguientes()
        {
            Zona("villa-luro", 50);
            for (var i = 1; i <= 7; i++)
                Zona($"vecina-{i}", i * 10);
            var pedido = Pedido();
            pedido.ZonaInicial = "vecina-7";

            var resultado = await _casoDeUso.EjecutarAsync(pedido);

            Assert.Equal(CalcularPuntajes.ZonasTopAutomatico, resultado.Zonas.Count);
            Assert.Contains(resultado.Zonas, zona => zona.Slug == "vecina-7");
            Assert.Equal(3, resultado.Cercanas.Count);
            Assert.DoesNotContain(resultado.Cercanas, zona => resultado.Zonas.Any(top => top.Slug == zona.Slug));
        }

        [Fact]
        public async Task Si_el_ruteo_no_responde_estima_el_viaje()
        {
            Zona("villa-luro", 30);
            _ruteo.Disponible = false;
            var pedido = Pedido();
            pedido.Lugares = [new LugarPuntajeRequest { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4, TiempoMax = "30 min" }];

            var lugar = Assert.Single(Assert.Single((await _casoDeUso.EjecutarAsync(pedido)).Zonas).Lugares);

            Assert.True(lugar.Estimado);
            Assert.Equal(30, lugar.MaximoMinutos);
            Assert.NotNull(lugar.Minutos);
            Assert.Null(lugar.DistanciaKm);
        }

        [Fact]
        public async Task Lee_el_tope_de_alquiler_como_lo_tipea_el_usuario()
        {
            Zona("villa-luro", 30);
            var pedido = Pedido();
            pedido.AlquilerMaximo = "$ 500.000";

            var zona = Assert.Single((await _casoDeUso.EjecutarAsync(pedido)).Zonas);

            var limite = Assert.Single(zona.Limites);
            Assert.False(limite.Cumple);
            Assert.Contains("500.000", limite.Detalle);
        }

        [Fact]
        public async Task Mide_los_viajes_segun_la_movilidad_del_hogar()
        {
            Zona("villa-luro", 30);
            var pedido = Pedido();
            pedido.Movilidad = ["vehiculo", "a-pie"];
            pedido.Lugares = [new LugarPuntajeRequest { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4 }];

            var resultado = await _casoDeUso.EjecutarAsync(pedido);

            Assert.Equal("auto", resultado.ModoViaje);
            Assert.Equal([ModoViaje.Auto], _ruteo.Modos.Distinct());
        }

        [Fact]
        public async Task ExecuteAsync_WhenZoneHasStartingPoint_MeasuresItsTripsFromThere()
        {
            Zona("villa-luro", 30);
            Zona("floresta", 30);
            var pedido = Pedido();
            pedido.Lugares = [new LugarPuntajeRequest { Nombre = "Trabajo", Latitude = -34.60, Longitude = -58.38 }];
            pedido.OrigenesZona = [new OrigenZonaRequest { Slug = "villa-luro", Latitude = -34.64, Longitude = -58.50 }];

            await _casoDeUso.EjecutarAsync(pedido);

            Assert.Contains((-34.64, -58.50), _ruteo.Origenes);
            Assert.Contains((-34.6, -58.5), _ruteo.Origenes);
        }

        [Fact]
        public async Task ExecuteAsync_WhenStartingPointIsOutsideItsZone_ThrowsBadRequestException()
        {
            Zona("villa-luro", 30);
            _zonas.Contiene = false;
            var pedido = Pedido();
            pedido.OrigenesZona = [new OrigenZonaRequest { Slug = "villa-luro", Latitude = -34.5, Longitude = -58.4 }];

            await Assert.ThrowsAsync<BadRequestException>(() => _casoDeUso.EjecutarAsync(pedido));
        }

        private sealed class FakeZonaRepository : IZonaRepository
        {
            public bool Contiene { get; set; } = true;

            public Task<string?> GetPorSlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default) =>
                Task.FromResult<string?>(null);

            public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult(true);

            public Task<bool> ContienePuntoAsync(string slug, double latitud, double longitud, CancellationToken cancellationToken = default) =>
                Task.FromResult(Contiene);

            public Task<IReadOnlyList<ZonaResumenDto>> BuscarAsync(string fragmento, int limite, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ZonaResumenDto>>([]);
        }
      
        [Theory]
        [InlineData("vehiculo", ModoViaje.Auto)]
        [InlineData("bici", ModoViaje.Bici)]
        [InlineData("a-pie", ModoViaje.APie)]
        [InlineData("transporte-publico", ModoViaje.TransportePublico)]
        public async Task Devuelve_la_distancia_del_recorrido_segun_la_movilidad(string movilidad, ModoViaje modo)
        {
            Zona("recoleta", 30);
            var pedido = Pedido();
            pedido.Movilidad = [movilidad];
            pedido.Lugares = [new LugarPuntajeRequest { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4 }];

            var resultado = await _casoDeUso.EjecutarAsync(pedido);
            var lugar = Assert.Single(Assert.Single(resultado.Zonas).Lugares);

            Assert.Equal(25, lugar.Minutos);
            Assert.Equal(8.75, lugar.DistanciaKm!.Value, 3);
            Assert.Equal([modo], _ruteo.Modos.Distinct());
        }

        [Fact]
        public async Task Lugar_sin_coordenadas_no_inventa_un_recorrido()
        {
            Zona("recoleta", 30);
            var pedido = Pedido();
            pedido.Lugares = [new LugarPuntajeRequest { Nombre = "Trabajo" }];

            var resultado = await _casoDeUso.EjecutarAsync(pedido);

            Assert.Empty(Assert.Single(resultado.Zonas).Lugares);
            Assert.Empty(_ruteo.Modos);
        }

        private sealed class FakePerfilesZonaRepository : IPerfilesZonaRepository
        {
            public List<PerfilZona> Zonas { get; } = [];

            public Task<IReadOnlyList<PerfilZona>> ListarAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<PerfilZona>>(Zonas);

            public Task<IReadOnlyList<string>> ListarZonasDelEntornoAsync(
                IReadOnlyCollection<(double Latitud, double Longitud)> ubicaciones,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(Zonas.Select(zone => zone.Slug).ToArray());
        }

        private sealed class FakeRuteoClient : IRuteoClient
        {
            public bool Disponible { get; set; } = true;
            public List<ModoViaje> Modos { get; } = [];
            public List<(double, double)> Origenes { get; } = [];

            public Task<TiempoViaje?> CalcularAsync(
                double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino, ModoViaje modo,
                CancellationToken cancellationToken = default)
            {
                lock (Modos)
                {
                    Modos.Add(modo);
                    Origenes.Add((latitudOrigen, longitudOrigen));
                }

                return Task.FromResult<TiempoViaje?>(Disponible ? new TiempoViaje(25, false, 8.75) : null);
            }
        }
    }
}
