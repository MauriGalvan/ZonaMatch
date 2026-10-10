using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Aportes
{
    public class ResumirAportesTests
    {
        private const string Zona = FakeZonaRepository.Zona;

        private readonly FakeAporteRepository _aportes = new();
        private readonly ResumirAportes _casoDeUso;

        public ResumirAportesTests()
        {
            _casoDeUso = new ResumirAportes(_aportes);
        }

        private void Agregar(EstadoAporte estado, string zona = Zona) =>
            _aportes.Agregar(new Aporte { ZonaSlug = zona, UsuarioId = Guid.NewGuid(), Estado = estado });

        [Fact]
        public async Task Resumir_CuentaAprobadosYPendientesSinLosRechazados()
        {
            Agregar(EstadoAporte.Aprobado);
            Agregar(EstadoAporte.Pendiente);
            Agregar(EstadoAporte.Pendiente);
            Agregar(EstadoAporte.Rechazado);
            Agregar(EstadoAporte.Aprobado, zona: "otra-zona");

            Assert.Equal(new ResumenAportesDto(3, 1, 2), await _casoDeUso.EjecutarAsync(Zona));
        }
    }
}
