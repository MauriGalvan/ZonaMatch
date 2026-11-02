using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Zones;

public sealed class GetZone(IZonaRepository zoneRepository)
{
    public async Task<string> ExecuteAsync(
        string slug,
        double? tolerance,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        return await zoneRepository.GetPorSlugAsync(
            slug, ZoneQueryPolicy.NormalizeBoundaryTolerance(tolerance), cancellationToken)
            ?? throw new NotFoundException("No se encontro una zona con ese identificador.");
    }
}
