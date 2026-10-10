using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Aportes
{
    public class ListarAportesPendientesTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _aportes = new();
        private readonly ListarAportesPendientes _casoDeUso;
        private readonly Usuario _autor = new() { Email = "ana.perez@example.com" };
        private readonly Guid _vecino = Guid.NewGuid();

        public ListarAportesPendientesTests()
        {
            _casoDeUso = new ListarAportesPendientes(_aportes);
        }

        private Aporte Agregar(EstadoAporte estado = EstadoAporte.Pendiente)
        {
            var aporte = new Aporte
            {
                ZonaSlug = Zona,
                UsuarioId = _autor.Id,
                Usuario = _autor,
                Tipo = TipoAporte.PuntoNuevo,
                Estado = estado,
                Nombre = "Farmacia Nueva",
                FechaCreacion = Ahora
            };
            _aportes.Agregar(aporte);
            return aporte;
        }

        [Fact]
        public async Task Listar_SoloPendientes_ConElVotoDelUsuario()
        {
            var pendiente = Agregar();
            pendiente.Validar(_vecino, confirma: false, Ahora);
            Agregar(EstadoAporte.Aprobado);

            var item = Assert.Single(await _casoDeUso.EjecutarAsync(Zona, _vecino));

            Assert.Equal(pendiente.Id, item.Id);
            Assert.False(item.MiVoto);
            Assert.False(item.EsMio);
            Assert.Equal("AP", item.Iniciales);
        }

        [Fact]
        public async Task Listar_ParaElAutor_IndicaQueEsSuyoYSinVoto()
        {
            Agregar();

            var item = Assert.Single(await _casoDeUso.EjecutarAsync(Zona, _autor.Id));

            Assert.True(item.EsMio);
            Assert.Null(item.MiVoto);
        }
    }
}
