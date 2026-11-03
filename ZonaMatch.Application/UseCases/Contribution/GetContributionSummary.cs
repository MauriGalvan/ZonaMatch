using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Contribution
{
    // Returns the total number of approved and pending contributions for a zone.
    public class GetContributionSummary
    {
        private readonly IContributionRepository _contributions;

        public GetContributionSummary(IContributionRepository contributions)
        {
            _contributions = contributions;
        }

        public async Task<ContributionSummaryResponse> ExecuteAsync(string zoneSlug, CancellationToken cancellationToken = default)
        {
            var (approved, pending) = await _contributions.CountAsync(zoneSlug, cancellationToken);
            return new ContributionSummaryResponse(approved + pending, approved, pending);
        }
    }
}
