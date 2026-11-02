namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>Sale price estimates in US dollars.</summary>
public sealed record SaleResponse(
    decimal AverageUsd,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? PricePerM2Usd = null,
    int? SurfaceM2 = null,
    decimal? EstimatedUsd = null);
