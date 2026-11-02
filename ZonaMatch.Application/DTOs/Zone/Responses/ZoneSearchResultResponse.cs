namespace ZonaMatch.Application.DTOs.Zone.Responses;

// A zone found by name, with the data needed to display it and open its details.
public sealed record ZoneSearchResultResponse(
    string Slug,
    string Name,
    int AdministrativeLevel,
    IReadOnlyList<JurisdictionResponse> Jurisdictions,
    double AreaKm2);
