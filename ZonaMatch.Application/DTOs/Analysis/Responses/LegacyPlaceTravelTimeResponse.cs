using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record LegacyPlaceTravelTimeResponse(
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("minutos")] int? Minutes,
    [property: JsonPropertyName("maximoMinutos")] int MaximumMinutes,
    [property: JsonPropertyName("estimado")] bool IsEstimated,
    double? DistanceKm = null);
