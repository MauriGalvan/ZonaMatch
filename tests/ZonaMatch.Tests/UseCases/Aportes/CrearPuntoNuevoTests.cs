using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Aportes
{
    public class CrearPuntoNuevoTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _aportes = new();
        private readonly CrearPuntoNuevo _casoDeUso;
        private readonly SesionDto _autor = new(Guid.NewGuid(), "ana.perez@example.com");

        public CrearPuntoNuevoTests()
        {
            _casoDeUso = new CrearPuntoNuevo(_aportes, new FakeZonaRepository(), new FixedTimeProvider(Ahora));
        }

        private static CrearPuntoNuevoDto Punto(string categoria = "salud", double latitud = -34.63) =>
            new(categoria, " Farmacia Nueva ", "Lun a vie 9 a 20", "  ", latitud, -58.50, ViveOTrabajaEnZona: true);

        [Fact]
        public async Task CrearPunto_GuardaPendienteConLosDatosLimpios()
        {
            var aporte = await _casoDeUso.EjecutarAsync(Zona, _autor, Punto(categoria: " Salud "));

            var guardado = Assert.Single(_aportes.Aportes);
            Assert.Equal(TipoAporte.PuntoNuevo, guardado.Tipo);
            Assert.Equal(EstadoAporte.Pendiente, guardado.Estado);
            Assert.Equal("salud", guardado.Categoria);
            Assert.Equal("Farmacia Nueva", guardado.Nombre);
            Assert.Null(guardado.Direccion);
            Assert.Equal(-34.63, guardado.Ubicacion!.Y);
            Assert.Equal(4326, guardado.Ubicacion.SRID);
            Assert.Equal(Ahora, guardado.FechaCreacion);
            Assert.Equal("AP", aporte.Iniciales);
            Assert.True(aporte.EsMio);
        }

        [Fact]
        public async Task CrearPunto_FueraDeLaZona_EsInvalido()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _casoDeUso.EjecutarAsync(Zona, _autor, Punto(latitud: 10)));
            Assert.Empty(_aportes.Aportes);
        }

        [Fact]
        public async Task CrearPunto_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _casoDeUso.EjecutarAsync("no-existe", _autor, Punto()));
            Assert.Empty(_aportes.Aportes);
        }

        [Fact]
        public async Task CrearPunto_CategoriaDesconocida_EsInvalida()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _casoDeUso.EjecutarAsync(Zona, _autor, Punto(categoria: "boliches")));
            Assert.Empty(_aportes.Aportes);
        }
    }
}
