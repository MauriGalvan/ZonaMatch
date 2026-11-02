namespace ZonaMatch.Application.DTOs.Zones.Responses;

// A zone found by name, with the data needed to display it and open its details.
public sealed record ZoneSearchResultResponse(
    string Slug,
    string Nombre,
    int NivelAdministrativo,
    IReadOnlyList<JurisdictionResponse> Jurisdicciones,
    double SuperficieKm2);
