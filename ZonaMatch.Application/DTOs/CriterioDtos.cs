namespace ZonaMatch.Application.DTOs
{
    // Respuestas del perfil y de los analisis, armadas desde las tablas normalizadas.
    // Los nombres de campos mantienen la forma que ya usaba el frontend.

    // GET /Perfil
    public record PerfilDto(
        ContextoDto Contexto,
        Dictionary<string, string> Criterios,
        PuntosDto Puntos,
        DateTimeOffset FechaActualizacion);

    // Un analisis guardado; en el listado, cada original trae sus versiones
    public record AnalisisDto(
        Guid Id,
        string Nombre,
        Guid? AnalisisOrigenId,
        IReadOnlyList<ZonaAnalisisDto> Zonas,
        ContextoDto Contexto,
        Dictionary<string, string> Criterios,
        PuntosDto Puntos,
        DateTimeOffset FechaCreacion,
        DateTimeOffset FechaActualizacion,
        IReadOnlyList<AnalisisDto> Versiones);

    public record ZonaAnalisisDto(string Slug, string Nombre, bool EsInicial);

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
