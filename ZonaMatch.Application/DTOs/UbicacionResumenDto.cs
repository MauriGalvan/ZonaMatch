namespace ZonaMatch.Application.DTOs
{
    // A point belongs to a Comuna (CABA) or to a Partido (rest of Buenos Aires), so both are optional
    public record UbicacionResumenDto(
        CoordenadaDto Ubicacion,
        PartidoDto? Partido,
        ComunaDto? Comuna,
        IReadOnlyList<EscuelaDto> EscuelasCercanas);
}
