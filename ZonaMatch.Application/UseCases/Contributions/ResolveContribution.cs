using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // A site administrator approves or rejects a pending contribution without waiting for the votes.
    // An approved one is persisted as a point of interest right away. The API checks the administrator role.
    public class ResolveContribution
    {
        private readonly IContributionRepository _contributions;
        private readonly CommunityPoiUpdater _poiUpdater;
        private readonly TimeProvider _clock;

        public ResolveContribution(IContributionRepository contributions, CommunityPoiUpdater poiUpdater, TimeProvider clock)
        {
            _contributions = contributions;
            _poiUpdater = poiUpdater;
            _clock = clock;
        }

        public async Task<ContributionResponse> ExecuteAsync(
            string zoneSlug, Guid contributionId, Guid administratorId, bool approve, CancellationToken cancellationToken = default)
        {
            var contribution = await _contributions.GetAsync(zoneSlug, contributionId, cancellationToken)
                ?? throw new NotFoundException("El aporte no existe.");

            if (contribution.Status != ContributionStatus.Pending)
                throw new ConflictException("El aporte ya fue resuelto.");

            var now = _clock.GetUtcNow();
            contribution.Resolve(administratorId, approve, now);
            await _poiUpdater.ApplyAsync(contribution, now, cancellationToken);
            await _contributions.SaveChangesAsync(cancellationToken);

            return ContributionMapper.ToResponse(contribution, contribution.User?.Email, administratorId);
        }
    }
}
