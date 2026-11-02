namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

public sealed record ZoneConditionsResponse(
    double? PorcentajeHogaresNbi,
    double? PorcentajeHogaresHacinamiento,
    double? PorcentajeHogaresAlquilan,
    double? PorcentajeCoberturaSalud,
    double? PorcentajeUniversitario);
