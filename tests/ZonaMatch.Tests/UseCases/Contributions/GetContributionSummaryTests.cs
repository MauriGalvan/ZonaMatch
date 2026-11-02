using ZonaMatch.Application.DTOs.Contributions.Responses;
using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contributions
{
    public class GetContributionSummaryTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;

        private readonly FakeContributionRepository _contributions = new();
        private readonly GetContributionSummary _useCase;

        public GetContributionSummaryTests()
        {
            _useCase = new GetContributionSummary(_contributions);
        }

        private void Add(ContributionStatus status, string zone = Zone) =>
            _contributions.Add(new Contribution { ZoneSlug = zone, UserId = Guid.NewGuid(), Status = status });

        [Fact]
        public async Task GetSummary_CountsApprovedAndPendingButNotRejectedContributions()
        {
            Add(ContributionStatus.Approved);
            Add(ContributionStatus.Pending);
            Add(ContributionStatus.Pending);
            Add(ContributionStatus.Rejected);
            Add(ContributionStatus.Approved, zone: "other-zone");

            Assert.Equal(new ContributionSummaryResponse(3, 1, 2), await _useCase.ExecuteAsync(Zone));
        }
    }
}
