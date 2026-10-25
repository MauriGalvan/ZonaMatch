using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // A site administrator approves or rejects a pending contribution without waiting for the votes.
    // An approved one is persisted as a point of interest right away. The API checks the administrator role.
    public class ResolveContribution
    {
        private readonly IAporteRepository _contributions;
        private readonly CommunityPoiUpdater _poiUpdater;
        private readonly TimeProvider _clock;

        public ResolveContribution(IAporteRepository contributions, CommunityPoiUpdater poiUpdater, TimeProvider clock)
        {
            _contributions = contributions;
            _poiUpdater = poiUpdater;
            _clock = clock;
        }

        public async Task<AporteDto> ExecuteAsync(
            string zoneSlug, Guid contributionId, Guid administratorId, bool approve, CancellationToken cancellationToken = default)
        {
            var contribution = await _contributions.ObtenerAsync(zoneSlug, contributionId, cancellationToken)
                ?? throw new NotFoundException("El aporte no existe.");

            if (contribution.Estado != EstadoAporte.Pendiente)
                throw new ConflictException("El aporte ya fue resuelto.");

            var now = _clock.GetUtcNow();
            contribution.Resolve(administratorId, approve, now);
            await _poiUpdater.ApplyAsync(contribution, now, cancellationToken);
            await _contributions.GuardarCambiosAsync(cancellationToken);

            return AporteMapeo.ADto(contribution, contribution.Usuario?.Email, administratorId);
        }
    }
}
