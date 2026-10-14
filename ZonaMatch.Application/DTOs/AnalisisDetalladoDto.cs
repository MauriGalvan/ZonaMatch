namespace ZonaMatch.Application.DTOs
{
    // Response for one saved analysis, rebuilt from the normalized tables.
    // The field names keep the shape the frontend already sends and reads.
    public record AnalisisDetalladoDto(
        Guid Id,
        string Nombre,
        ContextoDto Contexto,
        Dictionary<string, string> Criterios,
        PuntosDto Puntos,
        DateTimeOffset FechaCreacion,
        DateTimeOffset FechaActualizacion);

    public record ContextoDto(
        int Personas,
        IReadOnlyList<string?> Edades,
        IReadOnlyList<string> Movilidad,
        IReadOnlyList<string> Mascotas);

    public record PuntosDto(
        string? AlquilerMaximo,
        IReadOnlyList<LugarDto> Lugares);

    public record LugarDto(
        Guid Id,
        string Nombre,
        string? Direccion,
        double? Latitude,
        double? Longitude,
        string? Tipo,
        string? QuienVa,
        string? Frecuencia,
        string? Prioridad,
        string? TiempoMax,
        int Orden);
}
