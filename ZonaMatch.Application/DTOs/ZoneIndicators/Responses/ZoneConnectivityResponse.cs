namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

// When the zone is in CABA, the reference contains the city-level value for comparison.
public sealed record ZoneConnectivityResponse(
    double? PorcentajeHogaresInternet,
    double? PorcentajeHogaresComputadora,
    double? PorcentajeHogaresCelularInternet,
    IndicatorReferenceResponse? Referencia);
