namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

// Crime data is sourced from geolocated CABA events or province-level SNIC counts distributed by population.
public sealed record ZoneSafetyResponse(
    int Year,
    string Source,
    string? Scope,
    int Crimes,
    double? CrimesPer1000Residents,
    int Robberies,
    int Thefts,
    int Injuries,
    int Homicides,
    int Threats,
    int RoadIncidents,
    IndicatorReferenceResponse? Reference);
