using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record LegacySaleResponse(
    [property: JsonPropertyName("promedioUsd")] decimal AverageUsd,
    [property: JsonPropertyName("propiedadesDisponibles")] int AvailableProperties,
    [property: JsonPropertyName("expensas")] decimal? Expenses = null,
    [property: JsonPropertyName("precioM2Usd")] decimal? PricePerM2Usd = null,
    [property: JsonPropertyName("superficieM2")] int? SurfaceM2 = null,
    [property: JsonPropertyName("estimadoUsd")] decimal? EstimatedUsd = null);
