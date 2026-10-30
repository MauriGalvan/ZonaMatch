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
    int? Rooms = null);
