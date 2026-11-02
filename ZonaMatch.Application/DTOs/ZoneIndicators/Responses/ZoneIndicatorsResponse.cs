using ZonaMatch.Application.DTOs.Shared.Responses;

namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

// Open data for a zone. Percentages use one decimal place and may be null when there is no valid denominator.
public sealed record ZoneIndicatorsResponse(
    string Slug,
    string Nombre,
    int NivelAdministrativo,
    ZonePopulationResponse Poblacion,
    ZoneConnectivityResponse Conectividad,
    ZoneConditionsResponse Condiciones,
    // Null when the zone has no crime data.
    ZoneSafetyResponse? Seguridad,
    IReadOnlyList<DataSourceResponse> Fuentes);
