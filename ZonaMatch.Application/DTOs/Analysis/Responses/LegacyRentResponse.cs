using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record LegacyRentResponse(
    [property: JsonPropertyName("promedio")] decimal Average,
    [property: JsonPropertyName("propiedadesDisponibles")] int AvailableProperties,
    [property: JsonPropertyName("expensas")] decimal? Expenses = null,
    [property: JsonPropertyName("costoMensual")] decimal? MonthlyCost = null,
    [property: JsonPropertyName("precioM2")] decimal? PricePerM2 = null,
    [property: JsonPropertyName("superficieM2")] int? SurfaceM2 = null,
    [property: JsonPropertyName("estimado")] decimal? Estimated = null);
