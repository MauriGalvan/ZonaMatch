namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

public sealed record ZonePopulationResponse(
    int Total,
    double? DensidadHabitantesKm2,
    int Hogares,
    int Viviendas,
    double? PorcentajeMenores15,
    double? Porcentaje15a64,
    double? Porcentaje65yMas);
