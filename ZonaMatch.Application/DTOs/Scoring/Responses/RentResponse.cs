namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>Monthly rent estimates in Argentine pesos.</summary>
public sealed record RentResponse(
    decimal Average,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? MonthlyCost = null,
    decimal? PricePerM2 = null,
    int? SurfaceM2 = null,
    decimal? Estimated = null,
    // Number of rooms the prices are for (null: every size)
    int? Rooms = null,
    // Rent plus expenses at evenly spaced shares of the listings (21 points; 11 in older data)
    decimal[]? Distribution = null,
    // Share of the listings (0 to 1) priced up to each point of Distribution, ties included; null spaces them evenly
    decimal[]? DistributionShares = null);
