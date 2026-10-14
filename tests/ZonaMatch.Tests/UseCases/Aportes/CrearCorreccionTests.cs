using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Aportes
{
    public class CrearCorreccionTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _aportes = new();
        private readonly CrearCorreccion _casoDeUso;
        private readonly SesionDto _autor = new(Guid.NewGuid(), "ana.perez@example.com");

        public CrearCorreccionTests()
        {
            _casoDeUso = new CrearCorreccion(_aportes, new FakeZonaRepository(), new FixedTimeProvider(Ahora));
        }

        private static CrearCorreccionDto Correccion(
            MotivoCorreccion motivo, string? nombre = null, string? categoria = null,
            double? latitud = null, double? longitud = null, string? comentario = null) =>
            new("n123", "Kiosco Pepe", motivo, nombre, categoria, latitud, longitud, comentario);

        [Fact]
        public async Task Correccion_GuardaSoloElValorDelMotivo()
        {
            await _casoDeUso.EjecutarAsync(Zona, _autor,
                Correccion(MotivoCorreccion.Nombre, nombre: "Kiosco Pepito", categoria: "salud", latitud: -34.6, longitud: -58.5));

            var guardado = Assert.Single(_aportes.Aportes);
            Assert.Equal(TipoAporte.Correccion, guardado.Tipo);
            Assert.Equal(EstadoAporte.Pendiente, guardado.Estado);
            Assert.Equal("n123", guardado.PuntoInteresId);
            Assert.Equal("Kiosco Pepito", guardado.Nombre);
            Assert.Null(guardado.Categoria);
            Assert.Null(guardado.Ubicacion);
        }

        [Fact]
        public async Task Correccion_Ubicacion_GuardaElPuntoPropuesto()
        {
            await _casoDeUso.EjecutarAsync(Zona, _autor, Correccion(MotivoCorreccion.Ubicacion, latitud: -34.6, longitud: -58.5));

            var ubicacion = Assert.Single(_aportes.Aportes).Ubicacion!;
            Assert.Equal(-34.6, ubicacion.Y);
            Assert.Equal(-58.5, ubicacion.X);
        }

        [Theory]
        [InlineData(MotivoCorreccion.Nombre)]
        [InlineData(MotivoCorreccion.Categoria)]
        [InlineData(MotivoCorreccion.Ubicacion)]
        [InlineData(MotivoCorreccion.Otro)]
        public async Task Correccion_SinElDatoQuePideElMotivo_EsInvalida(MotivoCorreccion motivo)
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _casoDeUso.EjecutarAsync(Zona, _autor, Correccion(motivo)));
            Assert.Empty(_aportes.Aportes);
        }

        [Fact]
        public async Task Correccion_UbicacionFueraDeLaZona_EsInvalida()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(
                () => _casoDeUso.EjecutarAsync(Zona, _autor, Correccion(MotivoCorreccion.Ubicacion, latitud: 10, longitud: -58.5)));
            Assert.Empty(_aportes.Aportes);
        }

        [Fact]
        public async Task Correccion_Cerro_NoPideNadaMas()
        {
            await _casoDeUso.EjecutarAsync(Zona, _autor, Correccion(MotivoCorreccion.Cerro));

            Assert.Equal(MotivoCorreccion.Cerro, Assert.Single(_aportes.Aportes).Motivo);
        }

        [Fact]
        public async Task Correccion_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(
                () => _casoDeUso.EjecutarAsync("no-existe", _autor, Correccion(MotivoCorreccion.Cerro)));
            Assert.Empty(_aportes.Aportes);
        }
    }
}
