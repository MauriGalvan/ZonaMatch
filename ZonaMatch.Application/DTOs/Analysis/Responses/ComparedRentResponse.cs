namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record ComparedRentResponse(
    decimal Average,
    int AvailableProperties,
    decimal? Expenses = null,
    decimal? MonthlyCost = null,
    decimal? PricePerM2 = null,
    int? SurfaceM2 = null,
    decimal? Estimated = null,
    // Number of rooms the prices are for (null: every size)
    int? Rooms = null,
    // Rent plus expenses at every tenth of the listings (11 values)
    decimal[]? Distribution = null);
