using NetTopologySuite.Geometries;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Contribution;
using ZonaMatch.Domain.Entities;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;

namespace ZonaMatch.Tests.UseCases.Contribution
{
    public class ResolveContributionTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeContributionRepository _contributions = new();
        private readonly FakeCommunityPoiRepository _pois = new();
        private readonly ResolveContribution _useCase;
        private readonly Guid _administrator = Guid.NewGuid();

        public ResolveContributionTests()
        {
            _useCase = new ResolveContribution(_contributions, new CommunityPoiUpdater(_pois), new FixedTimeProvider(Now));
        }

        private DomainContribution Add(DomainContribution contribution)
        {
            contribution.ZoneSlug = Zone;
            contribution.UserId = Guid.NewGuid();
            _contributions.Add(contribution);
            return contribution;
        }

        private DomainContribution Correction(string poiId, CorrectionReason reason) =>
            Add(new DomainContribution { Type = ContributionType.Correction, PointOfInterestId = poiId, PointOfInterestName = "Kiosco", Reason = reason });

        [Fact]
        public async Task Execute_WhenApprovingANewPoint_PersistsItWithoutWaitingForVotes()
        {
            var contribution = Add(new DomainContribution
            {
                Type = ContributionType.NewPoint, Name = "Club Social", Category = "deporte",
                Location = new Point(-58.5, -34.63) { SRID = 4326 }
            });

            var result = await _useCase.ExecuteAsync(Zone, contribution.Id, _administrator, approve: true);

            Assert.Equal(ContributionStatus.Approved, result.Status);
            Assert.Equal(_administrator, contribution.ResolvedByUserId);
            Assert.Equal(Now, contribution.ResolvedAt);
            Assert.Equal(1, _contributions.SaveCount);
            var poi = Assert.Single(_pois.Pois);
            Assert.Equal("Club Social", poi.Name);
            Assert.Equal(contribution.Id, poi.LastContributionId);
        }

        [Fact]
        public async Task Execute_WhenRejecting_DoesNotPersistAPoint()
        {
            var contribution = Add(new DomainContribution { Type = ContributionType.NewPoint, Name = "Club", Category = "deporte" });

            var result = await _useCase.ExecuteAsync(Zone, contribution.Id, _administrator, approve: false);

            Assert.Equal(ContributionStatus.Rejected, result.Status);
            Assert.Empty(_pois.Pois);
        }

        [Fact]
        public async Task Execute_WhenApprovingCorrectionsToAnOsmPoint_KeepsEveryChangeOnTheSamePoint()
        {
            var rename = Correction("n123", CorrectionReason.Name);
            rename.Name = "Kiosco El Sol";
            var move = Correction("n123", CorrectionReason.Location);
            move.Location = new Point(-58.51, -34.64) { SRID = 4326 };

            await _useCase.ExecuteAsync(Zone, rename.Id, _administrator, approve: true);
            await _useCase.ExecuteAsync(Zone, move.Id, _administrator, approve: true);

            var poi = Assert.Single(_pois.Pois);
            Assert.Equal("n123", poi.Id);
            Assert.False(poi.IsNew);
            Assert.Equal("Kiosco El Sol", poi.Name);
            Assert.Equal(move.Location, poi.Location);
            Assert.Null(poi.Category);
            Assert.False(poi.Removed);
        }

        [Fact]
        public async Task Execute_WhenApprovingThatAPlaceClosed_RemovesIt()
        {
            var closed = Correction("w45", CorrectionReason.Closed);

            await _useCase.ExecuteAsync(Zone, closed.Id, _administrator, approve: true);

            Assert.True(Assert.Single(_pois.Pois).Removed);
        }

        [Fact]
        public async Task Execute_WhenApprovingCategoryCorrection_UpdatesTheExistingPointOfInterest()
        {
            var correction = Correction("n321", CorrectionReason.Category);
            correction.Category = "salud";

            await _useCase.ExecuteAsync(Zone, correction.Id, _administrator, approve: true);

            var poi = Assert.Single(_pois.Pois);
            Assert.Equal("n321", poi.Id);
            Assert.False(poi.IsNew);
            Assert.Equal("salud", poi.Category);
            Assert.Equal(correction.Id, poi.LastContributionId);
            Assert.Equal(Now, poi.UpdatedAt);
        }

        [Fact]
        public async Task Execute_WhenApprovingAnInformativeCorrection_ChangesNoPoint()
        {
            var other = Correction("n9", CorrectionReason.Other);
            other.Comment = "Abre solo de tarde";

            await _useCase.ExecuteAsync(Zone, other.Id, _administrator, approve: true);

            Assert.Empty(_pois.Pois);
        }

        [Fact]
        public async Task Execute_WhenAlreadyResolved_ThrowsConflictException()
        {
            var contribution = Add(new DomainContribution { Type = ContributionType.NewPoint, Status = ContributionStatus.Approved });

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
