using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record LegacyScoreLimitResponse(
    [property: JsonPropertyName("tipo")] string Type,
    [property: JsonPropertyName("cumple")] bool? IsMet,
    [property: JsonPropertyName("detalle")] string Details);
