using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contributions
{
    public class ValidateContributionTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeContributionRepository _contributions = new();
        private readonly FakeCommunityPoiRepository _pois = new();
        private readonly ValidateContribution _useCase;
        private readonly Guid _authorId = Guid.NewGuid();
        private readonly Contribution _contribution;

        public ValidateContributionTests()
        {
            _useCase = new ValidateContribution(_contributions, new CommunityPoiUpdater(_pois), new FixedTimeProvider(Now));
            _contribution = new Contribution
            {
                ZoneSlug = Zone,
                UserId = _authorId,
                User = new User { Id = _authorId, Email = "ana.perez@example.com" },
                Type = ContributionType.NewPoint,
                Category = "salud",
                Name = "Farmacia Nueva"
            };
            _contributions.Add(_contribution);
        }

        private static VoteOnContributionRequest Vote(bool confirm) => new(confirm);

        [Fact]
        public async Task Validate_WhenVotingOnOwnContribution_ThrowsConflictException()
        {
            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, _contribution.Id, _authorId, Vote(confirm: true)));
            Assert.Empty(_contribution.Votes);
        }

        [Fact]
        public async Task Validate_WhenEnoughNeighborsConfirm_ApprovesAndRejectsFurtherVotes()
        {
            for (var i = 0; i < Contribution.VoteDifferenceToResolve; i++)
                await _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: true));

            Assert.Equal(ContributionStatus.Approved, _contribution.Status);
            Assert.Equal(Now, _contribution.ResolvedAt);
            Assert.Equal(Contribution.VoteDifferenceToResolve, _contributions.SaveCount);
            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: false)));
        }

        [Fact]
        public async Task Validate_WhenTheVotesApproveANewPoint_PersistsItAsAPointOfInterest()
        {
            _contribution.Location = new NetTopologySuite.Geometries.Point(-58.5, -34.63) { SRID = 4326 };

            for (var i = 0; i < Contribution.VoteDifferenceToResolve; i++)
            {
                Assert.Empty(_pois.Pois);
                await _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: true));
            }

            var poi = Assert.Single(_pois.Pois);
            Assert.Equal("a" + _contribution.Id, poi.Id);
            Assert.True(poi.IsNew);
            Assert.Equal("Farmacia Nueva", poi.Name);
            Assert.Equal("salud", poi.Category);
            Assert.Equal(_contribution.Location, poi.Location);
        }

        [Fact]
        public async Task Validate_WhenTheVotesReject_DoesNotPersistAnything()
        {
            for (var i = 0; i < Contribution.VoteDifferenceToResolve; i++)
                await _useCase.ExecuteAsync(Zone, _contribution.Id, Guid.NewGuid(), Vote(confirm: false));

            Assert.Equal(ContributionStatus.Rejected, _contribution.Status);
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
