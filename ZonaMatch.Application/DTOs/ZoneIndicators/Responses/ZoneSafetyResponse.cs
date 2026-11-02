namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

// Crime data is sourced from geolocated CABA events or province-level SNIC counts distributed by population.
public sealed record ZoneSafetyResponse(
    int Anio,
    string Fuente,
    string? Ambito,
    int Delitos,
    double? DelitosCada1000Habitantes,
    int Robos,
    int Hurtos,
    int Lesiones,
    int Homicidios,
    int Amenazas,
    int SiniestrosViales,
    IndicatorReferenceResponse? Referencia);
