using ZonaMatch.Application.UseCases.Contribution;
using ZonaMatch.Domain.Entities;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;

namespace ZonaMatch.Tests.UseCases.Contribution
{
    public class ListPendingContributionsTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeContributionRepository _contributions = new();
        private readonly ListPendingContributions _useCase;
        private readonly User _author = new() { Email = "ana.perez@example.com" };
        private readonly Guid _neighborId = Guid.NewGuid();

        public ListPendingContributionsTests()
        {
            _useCase = new ListPendingContributions(_contributions);
        }

        private DomainContribution Add(ContributionStatus status = ContributionStatus.Pending)
        {
            var contribution = new DomainContribution
            {
                ZoneSlug = Zone,
                UserId = _author.Id,
                User = _author,
                Type = ContributionType.NewPoint,
                Status = status,
                Name = "Farmacia Nueva",
                CreatedAt = Now
            };
            _contributions.Add(contribution);
            return contribution;
        }

        [Fact]
        public async Task ListPending_WhenUserHasVoted_ReturnsPendingItemsWithTheirVote()
        {
            var pendingContribution = Add();
            pendingContribution.RecordVote(_neighborId, confirms: false, Now);
            Add(ContributionStatus.Approved);

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, _neighborId));

            Assert.Equal(pendingContribution.Id, item.Id);
            Assert.False(item.MyVote);
            Assert.False(item.IsMine);
            Assert.Equal("AP", item.AuthorInitials);
        }

        [Fact]
        public async Task ListPending_WhenUserIsTheAuthor_IndicatesOwnershipAndNoVote()
        {
            Add();

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, _author.Id));

            Assert.True(item.IsMine);
            Assert.Null(item.MyVote);
        }

        [Fact]
        public async Task ListPending_WhenThereAreNoPendingContributions_ReturnsAnEmptyList()
        {
            var contributions = await _useCase.ExecuteAsync(Zone, _neighborId);

            Assert.Empty(contributions);
        }
    }
}
