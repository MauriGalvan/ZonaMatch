using ZonaMatch.Application.DTOs.Group;

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

    public record ContextoDto(
        int Personas,
        IReadOnlyList<string> PersonNames,
        IReadOnlyList<string?> Edades,
        IReadOnlyList<string> Movilidad,
        IReadOnlyList<string> Mascotas);

    // AlquilerMaximo es el tope de gasto mensual (alquiler mas expensas, o solo expensas si compra)
    public record PuntosDto(
        string? AlquilerMaximo,
        IReadOnlyList<LugarDto> Lugares,
        string Operacion = "alquiler",
        decimal? CompraMaximaUsd = null,
        string? TipoVivienda = null);

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
