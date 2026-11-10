namespace ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

// Average sale price of the zone in dollars, listings for sale and monthly expenses in pesos
// With a surface, SurfaceM2 and EstimatedUsd (price for that surface).
// Rooms: the number of rooms the prices are for (null: every size)
public sealed record ZoneSaleResponse(
    decimal AverageUsd,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? PricePerM2Usd = null,
    int? SurfaceM2 = null,
    decimal? EstimatedUsd = null,
    int? Rooms = null,
    // Price in dollars at evenly spaced shares of the listings (21 points; 11 in older data)
    decimal[]? Distribution = null,
    // Share of the listings (0 to 1) priced up to each point of Distribution, ties included; null spaces them evenly
    decimal[]? DistributionShares = null,
    // Sales eligible for a mortgage ("apto crédito") the purchase cap is compared with, as in SaleResponse
    decimal[]? CreditDistribution = null,
    decimal[]? CreditDistributionShares = null,
    string? CreditPropertyType = null,
    int? CreditRooms = null,
    int? CreditListings = null);
