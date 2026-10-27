namespace ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

// Average sale price of the zone in dollars, listings for sale and monthly expenses in pesos
// With a surface, SurfaceM2 and EstimatedUsd (price for that surface)
public sealed record ZoneSaleResponse(
    decimal AverageUsd,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? PricePerM2Usd = null,
    int? SurfaceM2 = null,
    decimal? EstimatedUsd = null);
