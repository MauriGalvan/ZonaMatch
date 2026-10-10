using System.Text.Json;

namespace ZonaMatch.Application.DTOs
{
    // Response for one saved analysis. The payload fields are the raw jsonb documents
    // the frontend sent; they are echoed back verbatim.
    public record AnalisisDetalladoDto(
        Guid Id,
        string Nombre,
        JsonElement Contexto,
        JsonElement Criterios,
        JsonElement Puntos,
        DateTimeOffset FechaCreacion,
        DateTimeOffset FechaActualizacion);
}
