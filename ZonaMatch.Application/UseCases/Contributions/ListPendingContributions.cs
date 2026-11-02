using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // Lists contributions that are waiting for votes, including the current user's vote when available.
    public class ListPendingContributions
    {
        private readonly IContributionRepository _contributions;

        public ListPendingContributions(IContributionRepository contributions)
        {
            _contributions = contributions;
        }

        public async Task<IReadOnlyList<ContributionResponse>> ExecuteAsync(
            string zoneSlug, Guid? currentUserId, CancellationToken cancellationToken = default)
        {
            var contributions = await _contributions.ListPendingAsync(zoneSlug, cancellationToken);
            return contributions.Select(item => ContributionMapper.ToResponse(item, item.User?.Email, currentUserId)).ToList();
        }
    }
}
