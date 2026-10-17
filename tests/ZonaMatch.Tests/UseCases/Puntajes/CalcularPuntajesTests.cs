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
        private readonly CalcularPuntajes _casoDeUso;

        public CalcularPuntajesTests()
        {
            _casoDeUso = new CalcularPuntajes(_perfiles, _ruteo);
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

        private static CalcularPuntajesRequest Pedido(params string[] zonas) => new()
        {
            Zonas = [.. zonas],
            Criterios = new() { ["seguridad"] = "alta", ["inventado"] = "alta", ["salud"] = "urgente" },
        };

        [Fact]
        public async Task Sin_datos_de_ninguna_zona_avisa()
        {
            Zona("villa-luro", 30);

            await Assert.ThrowsAsync<NoEncontradoException>(() => _casoDeUso.EjecutarAsync(Pedido("no-existe")));
        }

        [Fact]
        public async Task Ignora_criterios_y_prioridades_desconocidos()
        {
            Zona("villa-luro", 30);

            var zona = Assert.Single((await _casoDeUso.EjecutarAsync(Pedido("villa-luro"))).Zonas);

            Assert.Equal(["seguridad"], zona.Componentes.Select(c => c.Criterio));
        }

        [Fact]
        public async Task Las_cercanas_no_repiten_analizadas_y_van_de_mejor_a_peor()
        {
            Zona("villa-luro", 50);
            for (var i = 1; i <= 7; i++)
                Zona($"vecina-{i}", i * 10);
            _perfiles.Vecinas.AddRange(["villa-luro", .. Enumerable.Range(1, 7).Select(i => $"vecina-{i}")]);

            var cercanas = (await _casoDeUso.EjecutarAsync(Pedido("villa-luro"))).Cercanas;

            Assert.Equal(CalcularPuntajes.VecinasDevueltas, cercanas.Count);
            Assert.DoesNotContain(cercanas, z => z.Slug == "villa-luro");
            Assert.Equal(["vecina-1", "vecina-2", "vecina-3", "vecina-4"], cercanas.Select(z => z.Slug));
        }

        [Fact]
        public async Task Si_el_ruteo_no_responde_estima_el_viaje()
        {
            Zona("villa-luro", 30);
            _ruteo.Disponible = false;
            var pedido = Pedido("villa-luro");
            pedido.Lugares = [new LugarPuntajeRequest { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4, TiempoMax = "30 min" }];

            var lugar = Assert.Single(Assert.Single((await _casoDeUso.EjecutarAsync(pedido)).Zonas).Lugares);

            Assert.True(lugar.Estimado);
            Assert.Equal(30, lugar.MaximoMinutos);
            Assert.NotNull(lugar.Minutos);
        }

        [Fact]
        public async Task Lee_el_tope_de_alquiler_como_lo_tipea_el_usuario()
        {
            Zona("villa-luro", 30);
            var pedido = Pedido("villa-luro");
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
            var pedido = Pedido("villa-luro");
            pedido.Movilidad = ["vehiculo", "a-pie"];
            pedido.Lugares = [new LugarPuntajeRequest { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4 }];

            var resultado = await _casoDeUso.EjecutarAsync(pedido);

            Assert.Equal("auto", resultado.ModoViaje);
            Assert.Equal([ModoViaje.Auto], _ruteo.Modos.Distinct());
        }

        private sealed class FakePerfilesZonaRepository : IPerfilesZonaRepository
        {
            public List<PerfilZona> Zonas { get; } = [];
            public List<string> Vecinas { get; } = [];

            public Task<IReadOnlyList<PerfilZona>> ListarAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<PerfilZona>>(Zonas);

            public Task<IReadOnlyList<string>> ListarVecinasAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(Vecinas);
        }

        private sealed class FakeRuteoClient : IRuteoClient
        {
            public bool Disponible { get; set; } = true;
            public List<ModoViaje> Modos { get; } = [];

            public Task<TiempoViaje?> CalcularAsync(
                double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino, ModoViaje modo,
                CancellationToken cancellationToken = default)
            {
                lock (Modos)
                    Modos.Add(modo);

                return Task.FromResult(Disponible ? new TiempoViaje(25, false) : null);
            }
        }
    }
}
