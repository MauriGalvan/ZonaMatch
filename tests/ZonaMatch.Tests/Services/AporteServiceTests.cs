using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Services
{
    public class AporteServiceTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _aportes = new();
        private readonly AporteService _sut;
        private readonly SesionDto _autor = new(Guid.NewGuid(), "ana.perez@example.com");

        public AporteServiceTests()
        {
            _sut = new AporteService(_aportes, new FakeZonaRepository(), new FixedTimeProvider(Ahora));
        }

        private static CrearPuntoNuevoDto Punto(string categoria = "salud", double latitud = -34.63) =>
            new(categoria, " Farmacia Nueva ", "Lun a vie 9 a 20", "  ", latitud, -58.50, ViveOTrabajaEnZona: true);

        private static CrearCorreccionDto Correccion(
            MotivoCorreccion motivo, string? nombre = null, string? categoria = null,
            double? latitud = null, double? longitud = null, string? comentario = null) =>
            new("n123", "Kiosco Pepe", motivo, nombre, categoria, latitud, longitud, comentario);

        [Fact]
        public async Task CrearPunto_GuardaPendienteConLosDatosLimpios()
        {
            var aporte = await _sut.CrearPuntoAsync(Zona, _autor, Punto());

            var guardado = Assert.Single(_aportes.Aportes);
            Assert.Equal(TipoAporte.PuntoNuevo, guardado.Tipo);
            Assert.Equal(EstadoAporte.Pendiente, guardado.Estado);
            Assert.Equal("Farmacia Nueva", guardado.Nombre);
            Assert.Null(guardado.Direccion);
            Assert.Equal(-34.63, guardado.Ubicacion!.Y);
            Assert.Equal(4326, guardado.Ubicacion.SRID);
            Assert.Equal("AP", aporte.Iniciales);
            Assert.True(aporte.EsMio);
        }

        [Fact]
        public async Task CrearPunto_FueraDeLaZona_EsInvalido()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _sut.CrearPuntoAsync(Zona, _autor, Punto(latitud: 10)));
            Assert.Empty(_aportes.Aportes);
        }

        [Fact]
        public async Task CrearPunto_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _sut.CrearPuntoAsync("no-existe", _autor, Punto()));
        }

        [Fact]
        public async Task CrearPunto_CategoriaDesconocida_EsInvalida()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _sut.CrearPuntoAsync(Zona, _autor, Punto(categoria: "boliches")));
        }

        [Fact]
        public async Task Correccion_GuardaSoloElValorDelMotivo()
        {
            await _sut.CrearCorreccionAsync(Zona, _autor,
                Correccion(MotivoCorreccion.Nombre, nombre: "Kiosco Pepito", categoria: "salud", latitud: -34.6, longitud: -58.5));

            var guardado = Assert.Single(_aportes.Aportes);
            Assert.Equal(TipoAporte.Correccion, guardado.Tipo);
            Assert.Equal("Kiosco Pepito", guardado.Nombre);
            Assert.Null(guardado.Categoria);
            Assert.Null(guardado.Ubicacion);
        }

        [Theory]
        [InlineData(MotivoCorreccion.Nombre)]
        [InlineData(MotivoCorreccion.Categoria)]
        [InlineData(MotivoCorreccion.Ubicacion)]
        [InlineData(MotivoCorreccion.Otro)]
        public async Task Correccion_SinElDatoQuePideElMotivo_EsInvalida(MotivoCorreccion motivo)
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _sut.CrearCorreccionAsync(Zona, _autor, Correccion(motivo)));
            Assert.Empty(_aportes.Aportes);
        }

        [Fact]
        public async Task Correccion_Cerro_NoPideNadaMas()
        {
            await _sut.CrearCorreccionAsync(Zona, _autor, Correccion(MotivoCorreccion.Cerro));

            Assert.Equal(MotivoCorreccion.Cerro, Assert.Single(_aportes.Aportes).Motivo);
        }

        [Fact]
        public async Task Correccion_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(
                () => _sut.CrearCorreccionAsync("no-existe", _autor, Correccion(MotivoCorreccion.Cerro)));
        }

        [Fact]
        public async Task Validar_TuPropioAporte_NoSePermite()
        {
            var aporte = await _sut.CrearPuntoAsync(Zona, _autor, Punto());

            await Assert.ThrowsAsync<OperacionNoPermitidaException>(
                () => _sut.ValidarAsync(Zona, aporte.Id, _autor.Id, confirma: true));
        }

        [Fact]
        public async Task Validar_TresVecinosConfirman_ApruebaYNoAceptaMasVotos()
        {
            var aporte = await _sut.CrearPuntoAsync(Zona, _autor, Punto());

            AporteDto? resultado = null;
            for (var i = 0; i < Aporte.DiferenciaParaResolver; i++)
                resultado = await _sut.ValidarAsync(Zona, aporte.Id, Guid.NewGuid(), confirma: true);

            Assert.Equal(EstadoAporte.Aprobado, resultado!.Estado);
            Assert.Equal(new ResumenAportesDto(1, 1, 0), await _sut.ResumirAsync(Zona));
            await Assert.ThrowsAsync<OperacionNoPermitidaException>(
                () => _sut.ValidarAsync(Zona, aporte.Id, Guid.NewGuid(), confirma: false));
        }

        [Fact]
        public async Task Validar_DevuelveElVotoDelUsuario()
        {
            var aporte = await _sut.CrearPuntoAsync(Zona, _autor, Punto());
            var vecino = Guid.NewGuid();

            var resultado = await _sut.ValidarAsync(Zona, aporte.Id, vecino, confirma: false);

            Assert.False(resultado.MiVoto);
            Assert.False(resultado.EsMio);
            Assert.Equal(1, resultado.Rechazos);
        }

        [Fact]
        public async Task Validar_AporteInexistente_NoEncontrado()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _sut.ValidarAsync(Zona, Guid.NewGuid(), Guid.NewGuid(), true));
        }
    }
}
