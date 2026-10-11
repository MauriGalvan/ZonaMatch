using System.Text.Json;
using ZonaMatch.Application.DTOs;
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
        public async Task Crear_CriteriosValidos_GuardaElJson()
        {
            var guardado = await _sut.CrearAsync(Usuario, Request("""{"seguridad":"alta","salud":"baja"}"""));

            var entidad = Assert.Single(_analisis.Guardados);
            Assert.Equal(entidad.Id, guardado.Id);
            Assert.Equal(Ahora, entidad.FechaCreacion);
            using var criterios = JsonDocument.Parse(entidad.CriteriosJson);
            Assert.Equal("alta", criterios.RootElement.GetProperty("seguridad").GetString());
            Assert.Equal("baja", criterios.RootElement.GetProperty("salud").GetString());
        }

        [Fact]
        public async Task Crear_CriterioDesconocido_LanzaErrorYNoGuarda()
        {
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request("""{"turismo":"alta"}""")));

            Assert.Contains("Criterio desconocido", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_CriterioInactivo_LanzaErrorDeNoDisponible()
        {
            // "transporte" existe en el catalogo pero esta desactivado
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request("""{"transporte":"alta"}""")));

            Assert.Contains("Criterio no disponible", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_PrioridadInvalida_LanzaError()
        {
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request("""{"seguridad":"urgentisima"}""")));

            Assert.Contains("Prioridad inválida", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Crear_CriteriosQueNoSonObjeto_LanzaError()
        {
            var excepcion = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.CrearAsync(Usuario, Request("""["seguridad"]""")));

            Assert.Contains("deben ser un objeto", excepcion.Message);
            Assert.Empty(_analisis.Guardados);
        }

        [Fact]
        public async Task Actualizar_CriterioDesconocido_NoModificaElAnalisis()
        {
            var creado = await _sut.CrearAsync(Usuario, Request("""{"seguridad":"alta"}"""));
            var original = Assert.Single(_analisis.Guardados).CriteriosJson;

            await Assert.ThrowsAsync<ArgumentException>(() => _sut.ActualizarAsync(
                creado.Id, Usuario, new ActualizarAnalisisRequest { Criterios = Json("""{"turismo":"alta"}""") }));

            Assert.Equal(original, _analisis.Guardados.Single().CriteriosJson);
        }

        private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement;

        private static CrearAnalisisRequest Request(string criterios) => new()
        {
            Nombre = "Familia Palermo",
            Contexto = Json("{}"),
            Criterios = Json(criterios),
            Puntos = Json("{}"),
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
            public List<AnalisisDetallado> Guardados { get; } = [];

            public Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guardados.Count(a => a.UsuarioId == usuarioId));

            public Task AddAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default)
            {
                Guardados.Add(analisis);
                return Task.CompletedTask;
            }

            public Task<AnalisisDetallado?> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guardados.FirstOrDefault(a => a.Id == id && a.UsuarioId == usuarioId));

            public Task<IReadOnlyList<AnalisisDetallado>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<AnalisisDetallado>>(Guardados.Where(a => a.UsuarioId == usuarioId).ToList());

            public Task UpdateAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task DeleteAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default)
            {
                Guardados.Remove(analisis);
                return Task.CompletedTask;
            }
        }
    }
}
