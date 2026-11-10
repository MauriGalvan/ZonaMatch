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
    // Sales eligible for a mortgage ("apto crédito") closest to the search: prices in dollars and the share of them
    // priced up to each one. With a mortgage the purchase cap is compared with these; null without credit data or
    // when the segment has none published.
    decimal[]? CreditDistribution = null,
    decimal[]? CreditDistributionShares = null,
    // The segment those sales are from: apartment, house, ph or all, and the rooms (null: every size)
    string? CreditPropertyType = null,
    int? CreditRooms = null,
    // Credit-eligible sales published in that segment (0: none; null in data loaded before the counts)
    int? CreditListings = null);
