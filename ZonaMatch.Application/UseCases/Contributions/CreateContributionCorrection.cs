using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.DTOs.Contributions.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // Suggests a map correction when a place closed or its name, location, or category is incorrect.
    public class CreateContributionCorrection
    {
        private readonly IAporteRepository _contributions;
        private readonly IZonaRepository _zones;
        private readonly TimeProvider _clock;

        public CreateContributionCorrection(IAporteRepository contributions, IZonaRepository zones, TimeProvider clock)
        {
            _contributions = contributions;
            _zones = zones;
            _clock = clock;
        }

        public async Task<ContributionResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext currentUser, CreateContributionCorrectionRequest request, CancellationToken cancellationToken = default)
        {
            var reason = request.Reason!.Value; // Required by request validation.

            var contribution = new Aporte
            {
                ZonaSlug = zoneSlug,
                UsuarioId = currentUser.Id,
                Tipo = TipoAporte.Correccion,
                PuntoInteresId = request.PointOfInterestId,
                PuntoInteresNombre = request.PointOfInterestName.Trim(),
                Motivo = reason,
                Comentario = ContributionMapper.Normalize(request.Comment),
                FechaCreacion = _clock.GetUtcNow()
            };

            // Store only the value associated with the selected reason.
            switch (reason)
            {
                case MotivoCorreccion.Nombre:
                    contribution.Nombre = ContributionMapper.Normalize(request.ProposedName)
                        ?? throw new BadRequestException("Indica el nombre correcto del lugar.");
                    break;

                case MotivoCorreccion.Categoria:
                    contribution.Categoria = ContributionMapper.ValidateCategory(request.ProposedCategory);
                    break;

                case MotivoCorreccion.Ubicacion:
                    if (request.Latitude is not { } latitude || request.Longitude is not { } longitude)
                        throw new BadRequestException("Marca en el mapa la ubicacion correcta del lugar.");
                    await _zones.ValidarContienePuntoAsync(zoneSlug, latitude, longitude, cancellationToken);
                    contribution.Ubicacion = ContributionMapper.CreatePoint(latitude, longitude);
                    break;

                case MotivoCorreccion.Otro when contribution.Comentario is null:
                    throw new BadRequestException("Contanos que hay que corregir en el comentario.");
            }

            // Location validation above already checks the zone; other reasons are validated here.
            if (reason != MotivoCorreccion.Ubicacion)
                await _zones.ValidarExisteAsync(zoneSlug, cancellationToken);

            _contributions.Agregar(contribution);
            await _contributions.GuardarCambiosAsync(cancellationToken);

            return ContributionMapper.ToResponse(contribution, currentUser.Email, currentUser.Id);
        }
    }
}
