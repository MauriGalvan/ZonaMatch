namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>Monthly rent estimates in Argentine pesos.</summary>
public sealed record RentResponse(
    decimal Average,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? MonthlyCost = null,
    decimal? PricePerM2 = null,
    int? SurfaceM2 = null,
    decimal? Estimated = null);
