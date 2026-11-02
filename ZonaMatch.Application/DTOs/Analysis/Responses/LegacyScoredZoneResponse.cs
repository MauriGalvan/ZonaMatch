using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

/// <summary>Preserves the response fields used by the legacy analysis comparison endpoint.</summary>
public sealed record LegacyScoredZoneResponse(
    string Slug,
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("puntaje")] int? Score,
    [property: JsonPropertyName("componentes")] IReadOnlyList<LegacyScoreComponentResponse> Components,
    [property: JsonPropertyName("limites")] IReadOnlyList<LegacyScoreLimitResponse> Limits,
    [property: JsonPropertyName("lugares")] IReadOnlyList<LegacyPlaceTravelTimeResponse> Places,
    [property: JsonPropertyName("alquiler")] LegacyRentResponse? Rent,
    [property: JsonPropertyName("compra")] LegacySaleResponse? Sale = null);
