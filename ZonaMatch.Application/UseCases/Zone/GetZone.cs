using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Zone;

public sealed class GetZone(IZoneRepository zoneRepository)
{
    public async Task<string> ExecuteAsync(
        string slug,
        double? tolerance,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        return await zoneRepository.GetBySlugAsync(
            slug, ZoneQueryPolicy.NormalizeBoundaryTolerance(tolerance), cancellationToken)
            ?? throw new NotFoundException("No se encontró una zona con ese identificador.");
    }
}
