using NetTopologySuite.Geometries;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Aportes
{
    public class ResolveContributionTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _contributions = new();
        private readonly FakeCommunityPoiRepository _pois = new();
        private readonly ResolveContribution _useCase;
        private readonly Guid _administrator = Guid.NewGuid();

        public ResolveContributionTests()
        {
            _useCase = new ResolveContribution(_contributions, new CommunityPoiUpdater(_pois), new FixedTimeProvider(Now));
        }

        private Aporte Add(Aporte contribution)
        {
            contribution.ZonaSlug = Zone;
            contribution.UsuarioId = Guid.NewGuid();
            _contributions.Agregar(contribution);
            return contribution;
        }

        private Aporte Correction(string poiId, MotivoCorreccion reason) =>
            Add(new Aporte { Tipo = TipoAporte.Correccion, PuntoInteresId = poiId, PuntoInteresNombre = "Kiosco", Motivo = reason });

        [Fact]
        public async Task Execute_WhenApprovingANewPoint_PersistsItWithoutWaitingForVotes()
        {
            var contribution = Add(new Aporte
            {
                Tipo = TipoAporte.PuntoNuevo, Nombre = "Club Social", Categoria = "deporte",
                Ubicacion = new Point(-58.5, -34.63) { SRID = 4326 }
            });

            var result = await _useCase.ExecuteAsync(Zone, contribution.Id, _administrator, approve: true);

            Assert.Equal(EstadoAporte.Aprobado, result.Estado);
            Assert.Equal(_administrator, contribution.ResueltoPorId);
            Assert.Equal(Now, contribution.FechaResolucion);
            Assert.Equal(1, _contributions.Guardados);
            var poi = Assert.Single(_pois.Pois);
            Assert.Equal("Club Social", poi.Name);
            Assert.Equal(contribution.Id, poi.LastContributionId);
        }

        [Fact]
        public async Task Execute_WhenRejecting_DoesNotPersistAPoint()
        {
            var contribution = Add(new Aporte { Tipo = TipoAporte.PuntoNuevo, Nombre = "Club", Categoria = "deporte" });

            var result = await _useCase.ExecuteAsync(Zone, contribution.Id, _administrator, approve: false);

            Assert.Equal(EstadoAporte.Rechazado, result.Estado);
            Assert.Empty(_pois.Pois);
        }

        [Fact]
        public async Task Execute_WhenApprovingCorrectionsToAnOsmPoint_KeepsEveryChangeOnTheSamePoint()
        {
            var rename = Correction("n123", MotivoCorreccion.Nombre);
            rename.Nombre = "Kiosco El Sol";
            var move = Correction("n123", MotivoCorreccion.Ubicacion);
            move.Ubicacion = new Point(-58.51, -34.64) { SRID = 4326 };

            await _useCase.ExecuteAsync(Zone, rename.Id, _administrator, approve: true);
            await _useCase.ExecuteAsync(Zone, move.Id, _administrator, approve: true);

            var poi = Assert.Single(_pois.Pois);
            Assert.Equal("n123", poi.Id);
            Assert.False(poi.IsNew);
            Assert.Equal("Kiosco El Sol", poi.Name);
            Assert.Equal(move.Ubicacion, poi.Location);
            Assert.Null(poi.Category);
            Assert.False(poi.Removed);
        }

        [Fact]
        public async Task Execute_WhenApprovingThatAPlaceClosed_RemovesIt()
        {
            var closed = Correction("w45", MotivoCorreccion.Cerro);

            await _useCase.ExecuteAsync(Zone, closed.Id, _administrator, approve: true);

            Assert.True(Assert.Single(_pois.Pois).Removed);
        }

        [Fact]
        public async Task Execute_WhenApprovingAnInformativeCorrection_ChangesNoPoint()
        {
            var other = Correction("n9", MotivoCorreccion.Otro);
            other.Comentario = "Abre solo de tarde";

            await _useCase.ExecuteAsync(Zone, other.Id, _administrator, approve: true);

            Assert.Empty(_pois.Pois);
        }

        [Fact]
        public async Task Execute_WhenAlreadyResolved_ThrowsConflictException()
        {
            var contribution = Add(new Aporte { Tipo = TipoAporte.PuntoNuevo, Estado = EstadoAporte.Aprobado });

            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, contribution.Id, _administrator, approve: false));
        }

        [Fact]
        public async Task Execute_WhenTheContributionDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync(Zone, Guid.NewGuid(), _administrator, approve: true));
        }
    }
}
