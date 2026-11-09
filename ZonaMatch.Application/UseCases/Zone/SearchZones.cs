using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Zone;

public sealed class SearchZones(IZoneRepository zoneRepository)
{
    public async Task<IReadOnlyList<ZoneSearchResultResponse>> ExecuteAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
            throw new BadRequestException("Indicá el texto a buscar.");

        if (limit is < 1 or > ZoneQueryPolicy.MaximumSearchResults)
            throw new BadRequestException(
                $"El limite debe estar entre 1 y {ZoneQueryPolicy.MaximumSearchResults}.");

        // Only letters and digits count: the text is turned into a slug, which drops spaces and punctuation
        if (search.Count(char.IsLetterOrDigit) < ZoneQueryPolicy.MinimumSearchCharacters)
            return [];

        var zones = await zoneRepository.SearchAsync(search, limit, cancellationToken);
        return zones
            .Select(zone => new ZoneSearchResultResponse(
                zone.Slug,
                zone.Name,
                zone.AdministrativeLevel,
                zone.Jurisdictions
                    .Select(jurisdiction => new JurisdictionResponse(jurisdiction.Name, jurisdiction.AdministrativeLevel))
                    .ToList(),
                zone.AreaKm2))
            .ToList();
    }
}
