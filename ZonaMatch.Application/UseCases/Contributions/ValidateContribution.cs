using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.DTOs.Contributions.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // Confirms or rejects another user's pending contribution; the vote can approve or reject it.
    // The vote that approves it persists it as a point of interest (CommunityPoiUpdater).
    public class ValidateContribution
    {
        private readonly IContributionRepository _contributions;
        private readonly CommunityPoiUpdater _poiUpdater;
        private readonly TimeProvider _clock;

        public ValidateContribution(IContributionRepository contributions, CommunityPoiUpdater poiUpdater, TimeProvider clock)
        {
            _contributions = contributions;
            _poiUpdater = poiUpdater;
            _clock = clock;
        }

        public async Task<ContributionResponse> ExecuteAsync(
            string zoneSlug, Guid contributionId, Guid userId, VoteOnContributionRequest request, CancellationToken cancellationToken = default)
        {
            var confirms = request.Confirm!.Value;
            var contribution = await _contributions.ObtenerAsync(zoneSlug, contributionId, cancellationToken)
                ?? throw new NotFoundException("El aporte no existe.");

            if (contribution.Estado != ContributionStatus.Pendiente)
                throw new ConflictException("El aporte ya fue resuelto por otros vecinos.");

            if (contribution.UsuarioId == userId)
                throw new ConflictException("No podes validar tu propio aporte.");

            var now = _clock.GetUtcNow();
            contribution.Validar(userId, confirms, now);
            await _poiUpdater.ApplyAsync(contribution, now, cancellationToken);
            await _contributions.GuardarCambiosAsync(cancellationToken);

            return ContributionMapper.ToResponse(contribution, contribution.User?.Email, userId);
        }
    }
}
