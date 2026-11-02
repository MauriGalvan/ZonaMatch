using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.UseCases.Zones;

public sealed class SearchZones(IZoneRepository zoneRepository)
{
    public async Task<IReadOnlyList<ZonaResumenDto>> ExecuteAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
            throw new BadRequestException("Indica el texto a buscar en buscar.");

        if (limit is < 1 or > ZoneQueryPolicy.MaximumSearchResults)
            throw new BadRequestException(
                $"El limite debe estar entre 1 y {ZoneQueryPolicy.MaximumSearchResults}.");

        var fragment = ZoneSlug.Create(search);
        if (fragment.Length < ZoneQueryPolicy.MinimumSearchCharacters)
            return [];

        return await zoneRepository.SearchAsync(fragment, limit, cancellationToken);
    }
}
