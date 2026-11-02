namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record ComparedSaleResponse(
    decimal AverageUsd,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? PricePerM2Usd = null,
    int? SurfaceM2 = null,
    decimal? EstimatedUsd = null,
    // Number of rooms the prices are for (null: every size)
    int? Rooms = null,
    // Price in dollars at every tenth of the listings (11 values)
    decimal[]? Distribution = null);
