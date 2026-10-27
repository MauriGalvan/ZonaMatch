namespace ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

// Average sale price of the zone in dollars, listings for sale and monthly expenses in pesos
public sealed record ZoneSaleResponse(decimal AverageUsd, int AvailableProperties, decimal? Expenses = null);
