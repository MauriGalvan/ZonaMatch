namespace ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

// Expenses: monthly average in pesos (null when unknown). MonthlyCost: rent plus expenses.
// With a surface, SurfaceM2 and Estimated (rent for that surface); MonthlyCost uses that estimate.
// Rooms: the number of rooms the prices are for (null: every size)
public sealed record ZoneRentResponse(
    decimal Average,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? MonthlyCost = null,
    decimal? PricePerM2 = null,
    int? SurfaceM2 = null,
    decimal? Estimated = null,
    int? Rooms = null,
    // Rent plus expenses at evenly spaced shares of the listings (21 points; 11 in older data)
    decimal[]? Distribution = null,
    // Share of the listings (0 to 1) priced up to each point of Distribution, ties included; null spaces them evenly
    decimal[]? DistributionShares = null);
