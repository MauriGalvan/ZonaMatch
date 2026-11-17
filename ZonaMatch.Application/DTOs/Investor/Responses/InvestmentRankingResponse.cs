using ZonaMatch.Application.DTOs.Scoring.Responses;

namespace ZonaMatch.Application.DTOs.Investor.Responses;

// Zones ranked for the investor profile, best first, for the property type and size of the profile: the automatic top
// and the zones the user added, plus the next best as recommendations.
public sealed record InvestmentRankingResponse(
    IReadOnlyList<InvestmentZoneResponse> Zones,
    IReadOnlyList<InvestmentZoneResponse> Recommendations,
    IReadOnlyDictionary<string, string> Criteria,
    decimal CapitalUsd,
    string? PropertyType,
    int? Rooms,
    bool CreditEligible,
    // Pesos per dollar used for the yield (the rate the market was loaded with)
    decimal UsdArs);

public sealed record InvestmentZoneResponse(
    string Slug,
    string Name,
    int? Score,
    InvestmentMetricsResponse Metrics,
    IReadOnlyList<ScoreComponentResponse> Components,
    IReadOnlyList<ScoreLimitResponse> Limits,
    SaleResponse? Sale);

// See ZonaMatch.Domain.Scoring.InvestmentMetrics. Average rent in pesos per month without expenses, sale in dollars.
public sealed record InvestmentMetricsResponse(
    double? GrossYieldPercent,
    double? PaybackYears,
    double? PriceBelowParentPercent,
    string? ParentName,
    int? SaleListings,
    double? SaleListingsPerThousandResidents,
    double? RentingHouseholdsPercentage,
    decimal? AverageRentArs,
    decimal? AverageSalePriceUsd);
