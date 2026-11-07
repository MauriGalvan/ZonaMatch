namespace ZonaMatch.Domain.Entities;

// Breakdown item representing a single penal crime category count and percentage.
public sealed record SafetyBreakdownItem(string Category, int Count, double Percentage);
