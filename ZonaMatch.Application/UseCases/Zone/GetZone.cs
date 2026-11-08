using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Zone;

public sealed class GetZone(IZoneRepository zoneRepository)
{
    // GeoJSON Feature of the zone (see ZoneGeoJson).
    public async Task<string> ExecuteAsync(
        string slug,
        double? tolerance,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        var zone = await zoneRepository.GetBySlugAsync(
            slug, ZoneQueryPolicy.NormalizeBoundaryTolerance(tolerance), cancellationToken)
            ?? throw new NotFoundException("No se encontró una zona con ese identificador.");

        return ZoneGeoJson.ToFeature(zone);
    }
}
