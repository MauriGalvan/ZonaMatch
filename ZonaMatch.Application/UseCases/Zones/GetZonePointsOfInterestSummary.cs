using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Zones;

public sealed class GetZonePointsOfInterestSummary(
    IPuntoInteresRepository pointOfInterestRepository,
    PointOfInterestResponseBuilder responseBuilder)
{
    public async Task<ResumenPuntosInteresDto> ExecuteAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        var counts = await pointOfInterestRepository.ContarPorTipoEnZonaAsync(slug, cancellationToken);
        return responseBuilder.BuildSummary(radiusMeters: null, counts: counts);
    }
}
