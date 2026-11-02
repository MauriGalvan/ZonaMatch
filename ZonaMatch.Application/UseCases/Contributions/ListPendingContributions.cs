using ZonaMatch.Application.DTOs.Contributions.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // Lists contributions that are waiting for votes, including the current user's vote when available.
    public class ListPendingContributions
    {
        private readonly IAporteRepository _contributions;

        public ListPendingContributions(IAporteRepository contributions)
        {
            _contributions = contributions;
        }

        public async Task<IReadOnlyList<ContributionResponse>> ExecuteAsync(
            string zoneSlug, Guid? currentUserId, CancellationToken cancellationToken = default)
        {
            var contributions = await _contributions.ListarPendientesAsync(zoneSlug, cancellationToken);
            return contributions.Select(item => ContributionMapper.ToResponse(item, item.Usuario?.Email, currentUserId)).ToList();
        }
    }
}
