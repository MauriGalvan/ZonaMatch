using ZonaMatch.Application.DTOs.Shared.Responses;

namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

// Open data for a zone. Percentages use one decimal place and may be null when there is no valid denominator.
public sealed record ZoneIndicatorsResponse(
    string Slug,
    string Name,
    int AdministrativeLevel,
    ZonePopulationResponse Population,
    ZoneConnectivityResponse Connectivity,
    ZoneConditionsResponse Conditions,
    // Null when the zone has no crime data.
    ZoneSafetyResponse? Safety,
    IReadOnlyList<DataSourceResponse> Sources);
