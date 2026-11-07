namespace ZonaMatch.Application.DTOs.Zone.Responses;

// Individual penal crime category count and percentage breakdown.
public sealed record SafetyBreakdownItemResponse(string Category, int Count, double Percentage);
