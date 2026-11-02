namespace ZonaMatch.Application.DTOs
{
    // A point belongs to a Commune (CABA) or to a District (rest of Buenos Aires), so both are optional
    public record UbicacionResumenDto(
        CoordenadaDto Ubicacion,
        PartidoDto? District,
        ComunaDto? Commune,
        IReadOnlyList<EscuelaDto> EscuelasCercanas);
}
