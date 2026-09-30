namespace ZonaMatch.Application.DTOs
{
    public record UbicacionResumenDto(
        CoordenadaDto Ubicacion,
        BarrioDto? Barrio,
        ZonaDto? Zona,
        IReadOnlyList<EscuelaDto> EscuelasCercanas,
        IReadOnlyList<EspacioVerdeDto> EspaciosVerdesCercanos);
}
