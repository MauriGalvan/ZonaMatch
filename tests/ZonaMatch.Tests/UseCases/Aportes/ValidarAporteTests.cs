using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Aportes
{
    public class ValidarAporteTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _aportes = new();
        private readonly ValidarAporte _casoDeUso;
        private readonly Guid _autor = Guid.NewGuid();
        private readonly Aporte _aporte;

        public ValidarAporteTests()
        {
            _casoDeUso = new ValidarAporte(_aportes, new FixedTimeProvider(Ahora));
            _aporte = new Aporte
            {
                ZonaSlug = Zona,
                UsuarioId = _autor,
                Usuario = new Usuario { Id = _autor, Email = "ana.perez@example.com" },
                Tipo = TipoAporte.PuntoNuevo,
                Categoria = "salud",
                Nombre = "Farmacia Nueva"
            };
            _aportes.Agregar(_aporte);
        }

        [Fact]
        public async Task Validar_TuPropioAporte_NoSePermite()
        {
            await Assert.ThrowsAsync<OperacionNoPermitidaException>(
                () => _casoDeUso.EjecutarAsync(Zona, _aporte.Id, _autor, confirma: true));
            Assert.Empty(_aporte.Validaciones);
        }

        [Fact]
        public async Task Validar_TresVecinosConfirman_ApruebaYNoAceptaMasVotos()
        {
            for (var i = 0; i < Aporte.DiferenciaParaResolver; i++)
                await _casoDeUso.EjecutarAsync(Zona, _aporte.Id, Guid.NewGuid(), confirma: true);

            Assert.Equal(EstadoAporte.Aprobado, _aporte.Estado);
            Assert.Equal(Ahora, _aporte.FechaResolucion);
            Assert.Equal(Aporte.DiferenciaParaResolver, _aportes.Guardados);
            await Assert.ThrowsAsync<OperacionNoPermitidaException>(
                () => _casoDeUso.EjecutarAsync(Zona, _aporte.Id, Guid.NewGuid(), confirma: false));
        }

        [Fact]
        public async Task Validar_DevuelveElVotoDelUsuario()
        {
            var resultado = await _casoDeUso.EjecutarAsync(Zona, _aporte.Id, Guid.NewGuid(), confirma: false);

            Assert.False(resultado.MiVoto);
            Assert.False(resultado.EsMio);
            Assert.Equal(1, resultado.Rechazos);
            Assert.Equal("AP", resultado.Iniciales);
        }

        [Fact]
        public async Task Validar_AporteInexistente_NoEncontrado()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _casoDeUso.EjecutarAsync(Zona, Guid.NewGuid(), Guid.NewGuid(), true));
        }
    }
}
