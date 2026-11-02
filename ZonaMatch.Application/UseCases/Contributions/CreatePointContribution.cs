using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.DTOs.Contributions.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // Suggests a place missing from the map; it must be inside the zone.
    public class CreatePointContribution
    {
        private readonly IContributionRepository _contributions;
        private readonly IZoneRepository _zones;
        private readonly TimeProvider _clock;

        public CreatePointContribution(IContributionRepository contributions, IZoneRepository zones, TimeProvider clock)
        {
            _contributions = contributions;
            _zones = zones;
            _clock = clock;
        }

        public async Task<ContributionResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext currentUser, CreatePointContributionRequest request, CancellationToken cancellationToken = default)
        {
            var category = ContributionMapper.ValidateCategory(request.Category);
            await _zones.ValidarContienePuntoAsync(zoneSlug, request.Latitude, request.Longitude, cancellationToken);

            var contribution = new Contribution
            {
                ZonaSlug = zoneSlug,
                UsuarioId = currentUser.Id,
                Tipo = ContributionType.PuntoNuevo,
                Categoria = category,
                Nombre = request.Name.Trim(),
                Ubicacion = ContributionMapper.CreatePoint(request.Latitude, request.Longitude),
                Horario = ContributionMapper.Normalize(request.Schedule),
                Direccion = ContributionMapper.Normalize(request.Address),
                ViveOTrabajaEnZona = request.LivesOrWorksInZone,
                FechaCreacion = _clock.GetUtcNow()
            };

            _contributions.Agregar(contribution);
            await _contributions.GuardarCambiosAsync(cancellationToken);

            return ContributionMapper.ToResponse(contribution, currentUser.Email, currentUser.Id);
        }
    }
}
