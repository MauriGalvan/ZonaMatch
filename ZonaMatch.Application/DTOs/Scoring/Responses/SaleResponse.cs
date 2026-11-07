namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>Sale price estimates in US dollars.</summary>
public sealed record SaleResponse(
    decimal AverageUsd,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? PricePerM2Usd = null,
    int? SurfaceM2 = null,
    decimal? EstimatedUsd = null,
    // Number of rooms the prices are for (null: every size)
    int? Rooms = null,
    // Price in dollars at evenly spaced shares of the listings (21 points; 11 in older data)
    decimal[]? Distribution = null,
    // Share of the listings (0 to 1) priced up to each point of Distribution, ties included; null spaces them evenly
    decimal[]? DistributionShares = null,
    // Sales eligible for a mortgage ("apto crédito"), every type: prices in dollars and the share of them priced up to
    // each one. With a mortgage the purchase cap is compared with these; null without credit data.
    decimal[]? CreditDistribution = null,
    decimal[]? CreditDistributionShares = null);
