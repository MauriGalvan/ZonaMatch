using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases;

namespace ZonaMatch.Tests.Services
{
    public class AnalisisServiceTests
    {
        private static readonly DateTimeOffset Ahora = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid Usuario = Guid.NewGuid();

        private readonly FakeAnalisisRepository _analisis = new();
        private readonly AnalisisService _sut;

        public AnalisisServiceTests()
        {
            _sut = new AnalisisService(_analisis, new FakeCriterioRepository(), new FixedTimeProvider(Ahora));
        }

        [Fact]
        public async Task Crear_CriteriosValidos_GuardaLasFilas()
        {
            var guardado = await _sut.CrearAsync(Usuario, Request(new() { ["seguridad"] = "alta", ["salud"] = "baja" }));

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal(entidad.Id, guardado.Id);
            Assert.Equal(Ahora, entidad.FechaCreacion);

            Assert.Equal(2, entidad.Criterios.Count);
            Assert.Contains(entidad.Criterios, c => c.CriterioId == "seguridad" && c.Prioridad == "alta");
            Assert.Contains(entidad.Criterios, c => c.CriterioId == "salud" && c.Prioridad == "baja");

            Assert.Equal("alta", guardado.Criterios["seguridad"]);
            Assert.Equal("baja", guardado.Criterios["salud"]);
        }

        [Fact]
        public async Task Crear_CriterioDesconocido_LanzaErrorYNoGuarda()
        {
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request(new() { ["turismo"] = "alta" })));

            Assert.Contains("Criterio desconocido", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_CriterioInactivo_LanzaErrorDeNoDisponible()
        {
            // "transporte" existe en el catalogo pero esta desactivado
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request(new() { ["transporte"] = "alta" })));

            Assert.Contains("Criterio no disponible", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_PrioridadInvalida_LanzaError()
        {
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request(new() { ["seguridad"] = "urgentisima" })));

            Assert.Contains("Prioridad inválida", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_PrioridadNula_LanzaError()
        {
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request(new() { ["seguridad"] = null })));

            Assert.Contains("Prioridad inválida", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_PersonasYEdades_CreaUnaFilaPorIntegrante()
        {
            var contexto = new ContextoRequest
            {
                Personas = 3,
                Edades = ["26-35 años", null, "36-45 años"],
            };

            var dto = await _sut.CrearAsync(Usuario, Request(new(), contexto));

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal(3, entidad.Personas);
            Assert.Equal(3, entidad.Integrantes.Count);

            var integrantes = entidad.Integrantes.OrderBy(p => p.Orden).ToList();
            Assert.Equal("26-35 años", integrantes[0].Edad);
            Assert.Null(integrantes[1].Edad);
            Assert.Equal("36-45 años", integrantes[2].Edad);

            Assert.Equal(new string?[] { "26-35 años", null, "36-45 años" }, dto.Contexto.Edades);
        }

        [Fact]
        public async Task Crear_PersonasFueraDeRango_LanzaError()
        {
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request(new(), new ContextoRequest { Personas = 9 })));

            Assert.Contains("cantidad de personas", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_MasEdadesQuePersonas_LanzaError()
        {
            var contexto = new ContextoRequest { Personas = 1, Edades = ["26-35 años", "36-45 años"] };

            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request(new(), contexto)));

            Assert.Contains("más edades que personas", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_Movilidad_GuardaLoQueElUsuarioEligioSinRepetir()
        {
            var contexto = new ContextoRequest
            {
                Personas = 1,
                Movilidad = ["a-pie", "bici", "a-pie"],
                Mascotas = ["perro", "gato"],
            };

            var dto = await _sut.CrearAsync(Usuario, Request(new(), contexto));

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal(new[] { "a-pie", "bici" }, entidad.Movilidad);
            Assert.Equal(new[] { "perro", "gato" }, entidad.Mascotas);

            Assert.Equal(new[] { "a-pie", "bici" }, dto.Contexto.Movilidad);
            Assert.Equal(new[] { "perro", "gato" }, dto.Contexto.Mascotas);
        }

        [Fact]
        public async Task Crear_MovilidadSeGuardaVerbatim_SinCatalogoDePorMedio()
        {
            // Movilidad es una lista simple como mascotas: no hay catalogo que validar
            var contexto = new ContextoRequest { Personas = 1, Movilidad = ["monopatin", "a-pie"] };

            var dto = await _sut.CrearAsync(Usuario, Request(new(), contexto));

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal(new[] { "monopatin", "a-pie" }, entidad.Movilidad);
            Assert.Equal(new[] { "monopatin", "a-pie" }, dto.Contexto.Movilidad);
        }

        [Fact]
        public async Task Crear_Lugares_GuardaFilasConOrden()
        {
            var puntos = new PuntosRequest
            {
                AlquilerMaximo = "450000",
                Lugares =
                [
                    new LugarRequest
                    {
                        Nombre = "Trabajo",
                        Tipo = "Trabajo",
                        Direccion = "Av. Siempreviva 742",
                        Latitude = -34.6,
                        Longitude = -58.4,
                        QuienVa = "Yo",
                        Frecuencia = "5x por semana",
                        Prioridad = "Alta",
                        TiempoMax = "45 min",
                    },
                    new LugarRequest { Nombre = "Colegio" },
                ],
            };

            var dto = await _sut.CrearAsync(Usuario, Request(new(), null, puntos));

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal("450000", entidad.AlquilerMaximo);
            Assert.Equal(2, entidad.Puntos.Count);

            var lugares = entidad.Puntos.OrderBy(p => p.Orden).ToList();
            Assert.Equal(1, lugares[0].Orden);
            Assert.Equal("Trabajo", lugares[0].Nombre);
            Assert.Equal(-34.6, lugares[0].Latitude);
            Assert.Equal(2, lugares[1].Orden);
            Assert.Equal("Colegio", lugares[1].Nombre);

            Assert.Equal(2, dto.Puntos.Lugares.Count);
            Assert.Equal("450000", dto.Puntos.AlquilerMaximo);
        }

        [Fact]
        public async Task Crear_LugarSinNombre_LanzaError()
        {
            var puntos = new PuntosRequest { Lugares = [new LugarRequest { Nombre = "   " }] };

            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request(new(), null, puntos)));

            Assert.Contains("nombre del lugar", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_CuandoYaTieneTres_LanzaLimiteAnalisis()
        {
            for (var i = 0; i < AnalisisService.MaximoPorUsuario; i++)
                await _sut.CrearAsync(Usuario, Request(new()));

            await Assert.ThrowsAsync<LimiteAnalisisException>(
                () => _sut.CrearAsync(Usuario, Request(new())));
        }

        [Fact]
        public async Task Actualizar_CriterioDesconocido_NoModificaElAnalisis()
        {
            var creado = await _sut.CrearAsync(Usuario, Request(new() { ["seguridad"] = "alta" }));

            await Assert.ThrowsAsync<ArgumentException>(() => _sut.ActualizarAsync(
                creado.Id, Usuario, new ActualizarAnalisisRequest
                {
                    Criterios = new() { ["turismo"] = "alta" },
                }));

            var entidad = Assert.Single(_analisis.Guardados);
            var criterio = Assert.Single(entidad.Criterios);
            Assert.Equal("seguridad", criterio.CriterioId);
            Assert.Equal("alta", criterio.Prioridad);
        }

        [Fact]
        public async Task Actualizar_Contexto_ReemplazaIntegrantesYDejaElResto()
        {
            var creado = await _sut.CrearAsync(
                Usuario,
                Request(
                    new() { ["seguridad"] = "alta" },
                    new ContextoRequest { Personas = 3, Edades = ["18-25 años", "26-35 años", "36-45 años"] }));

            await _sut.ActualizarAsync(creado.Id, Usuario, new ActualizarAnalisisRequest
            {
                Contexto = new ContextoRequest { Personas = 1, Edades = ["46-60 años"] },
            });

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal(1, entidad.Personas);
            var integrante = Assert.Single(entidad.Integrantes);
            Assert.Equal(1, integrante.Orden);
            Assert.Equal("46-60 años", integrante.Edad);

            // El bloque de criterios no se toco
            var criterio = Assert.Single(entidad.Criterios);
            Assert.Equal("seguridad", criterio.CriterioId);
        }

        [Fact]
        public async Task Actualizar_SinBloques_SoloMantieneElNombre()
        {
            var creado = await _sut.CrearAsync(Usuario, Request(new() { ["seguridad"] = "media" }));

            await _sut.ActualizarAsync(creado.Id, Usuario, new ActualizarAnalisisRequest
            {
                Nombre = "  Otro nombre  ",
            });

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal("Otro nombre", entidad.Nombre);
            Assert.Single(entidad.Criterios);
            Assert.Single(entidad.Integrantes);
        }

        private static CrearAnalisisRequest Request(
            Dictionary<string, string?> criterios,
            ContextoRequest? contexto = null,
            PuntosRequest? puntos = null) => new()
        {
            Nombre = "Familia Palermo",
            Contexto = contexto ?? new ContextoRequest { Personas = 1, Edades = ["26-35 años"] },
            Criterios = criterios,
            Puntos = puntos ?? new PuntosRequest(),
        };

        // Catalogo: dos criterios activos y uno desactivado
        private sealed class FakeCriterioRepository : ICriterioRepository
        {
            private static readonly IReadOnlyList<Criterio> Catalogo =
            [
                new() { Id = "seguridad", Nombre = "Seguridad", Descripcion = "Delitos y percepción de seguridad", Icono = "pi pi-shield", Orden = 1 },
                new() { Id = "salud", Nombre = "Salud", Descripcion = "Hospitales y salitas", Icono = "pi pi-heart", Orden = 2 },
                new() { Id = "transporte", Nombre = "Transporte", Descripcion = "Desactivado", Icono = "pi pi-truck", Orden = 3, Activo = false },
            ];

            public Task<IReadOnlyList<Criterio>> ListarAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(Catalogo);
        }

        private sealed class FakeAnalisisRepository : IAnalisisRepository
        {
            public List<Contexto> Guardados { get; } = [];

            public Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guardados.Count(c => c.UsuarioId == usuarioId));

            public Task AddAsync(Contexto contexto, CancellationToken cancellationToken = default)
            {
                Guardados.Add(contexto);
                return Task.CompletedTask;
            }

            public Task<Contexto?> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guardados.FirstOrDefault(c => c.Id == id && c.UsuarioId == usuarioId));

            public Task<IReadOnlyList<Contexto>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<Contexto>>(Guardados.Where(c => c.UsuarioId == usuarioId).ToList());

            public Task UpdateAsync(Contexto contexto, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task DeleteAsync(Contexto contexto, CancellationToken cancellationToken = default)
            {
                Guardados.Remove(contexto);
                return Task.CompletedTask;
            }
        }
    }
}
