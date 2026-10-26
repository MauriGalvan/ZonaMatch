namespace ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

// Expenses: monthly average in pesos (null when unknown). MonthlyCost: rent plus expenses.
public sealed record ZoneRentResponse(decimal Average, int AvailableProperties, decimal? Expenses = null, decimal? MonthlyCost = null);
