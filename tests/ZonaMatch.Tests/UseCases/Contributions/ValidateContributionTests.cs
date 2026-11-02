using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contributions
{
    public class ValidateContributionTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _contributions = new();
        private readonly FakeCommunityPoiRepository _pois = new();
        private readonly ValidateContribution _useCase;
        private readonly Guid _authorId = Guid.NewGuid();
        private readonly Contribution _contribution;

        public ValidateContributionTests()
        {
            _useCase = new ValidateContribution(_contributions, new CommunityPoiUpdater(_pois), new FixedTimeProvider(Now));
            _contribution = new Contribution
            {
                ZonaSlug = Zone,
                UsuarioId = _authorId,
                User = new User { Id = _authorId, Email = "ana.perez@example.com" },
                Tipo = ContributionType.PuntoNuevo,
                Categoria = "salud",
                Nombre = "Farmacia Nueva"
            };
            _contributions.Agregar(_contribution);
        }

        private static VoteOnContributionRequest Vote(bool confirm) => new(confirm);

        [Fact]
        public async Task Validate_WhenVotingOnOwnContribution_ThrowsConflictException()
        {
            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, _contribution.Id, _authorId, Vote(confirm: true)));
            Assert.Empty(_contribution.Validaciones);
        }

        [Fact]
        public async Task Validate_WhenEnoughNeighborsConfirm_ApprovesAndRejectsFurtherVotes()
        {
            for (var i = 0; i < Contribution.DiferenciaParaResolver; i++)
                await _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: true));

            Assert.Equal(ContributionStatus.Aprobado, _contribution.Estado);
            Assert.Equal(Now, _contribution.FechaResolucion);
            Assert.Equal(Contribution.DiferenciaParaResolver, _contributions.Guardados);
            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: false)));
        }

        [Fact]
        public async Task Validate_WhenTheVotesApproveANewPoint_PersistsItAsAPointOfInterest()
        {
            _contribution.Ubicacion = new NetTopologySuite.Geometries.Point(-58.5, -34.63) { SRID = 4326 };

            for (var i = 0; i < Contribution.DiferenciaParaResolver; i++)
            {
                Assert.Empty(_pois.Pois);
                await _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: true));
            }

            var poi = Assert.Single(_pois.Pois);
            Assert.Equal("a" + _contribution.Id, poi.Id);
            Assert.True(poi.IsNew);
            Assert.Equal("Farmacia Nueva", poi.Name);
            Assert.Equal("salud", poi.Category);
            Assert.Equal(_contribution.Ubicacion, poi.Location);
        }

        [Fact]
        public async Task Validate_WhenTheVotesReject_DoesNotPersistAnything()
        {
            for (var i = 0; i < Contribution.DiferenciaParaResolver; i++)
                await _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: false));

            Assert.Equal(ContributionStatus.Rechazado, _contribution.Estado);
            Assert.Empty(_pois.Pois);
        }

        [Fact]
        public async Task Validate_ReturnsTheCurrentUsersVote()
        {
            var response = await _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: false));

            Assert.False(response.MyVote);
            Assert.False(response.IsMine);
            Assert.Equal(1, response.Rejections);
            Assert.Equal("AP", response.AuthorInitials);
        }

        [Fact]
        public async Task Validate_WhenContributionDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(Zone, Guid.NewGuid(), Guid.NewGuid(), Vote(confirm: true)));
        }
    }
}
