using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record LegacyScoreComponentResponse(
    [property: JsonPropertyName("criterio")] string Criterion,
    [property: JsonPropertyName("prioridad")] string Priority,
    [property: JsonPropertyName("puntaje")] int? Score,
    [property: JsonPropertyName("detalle")] string Details);
