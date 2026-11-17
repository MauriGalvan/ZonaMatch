namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/zones: how many zones have each kind of data, and every zone for the admin zone selector.
public sealed record AdminZoneOverviewResponse(AdminZoneCoverage Coverage, IReadOnlyList<AdminZoneSummaryResponse> Zones);

// Zones with data of each kind. Small zones are neighborhoods and localities; large ones, comunas and partidos.
public sealed record AdminZoneCoverage(
    int Zones,
    int SmallZones,
    int LargeZones,
    int WithSafety,
    int WithRent,
    int WithSale,
    int WithCreditSales);

// Score: the zone's score with the five criteria at the same priority (0 to 100), as the analysis calculates it.
// CreditListings: sales eligible for a mortgage ("apto crédito") published in the zone; null without credit data.
public sealed record AdminZoneSummaryResponse(
    string Slug,
    string Name,
    int AdministrativeLevel,
    bool IsLargeZone,
    int Population,
    int? Score,
    decimal? AverageRent,
    decimal? AverageSalePriceUsd,
    int? CreditListings);

// GET /api/v1/admin/zones/{slug}: the analysis breakdown of one zone, its market, and its credit-eligible sales.
public sealed record AdminZoneDetailResponse(
    string Slug,
    string Name,
    int AdministrativeLevel,
    bool IsLargeZone,
    int Population,
    double AreaKm2,
    int? Score,
    IReadOnlyList<AdminCriterionScoreResponse> Criteria,
    AdminZoneMarketResponse Market,
    AdminZoneCreditResponse? Credit);

// Percentile of the zone among zones of its size (0 to 100; null without data) and the figure it comes from.
public sealed record AdminCriterionScoreResponse(string Criterion, int? Score, string Details);

// Rent and expenses in Argentine pesos per month; sale prices in US dollars. Null when unavailable.
public sealed record AdminZoneMarketResponse(
    decimal? AverageRent,
    int? RentListings,
    decimal? AverageExpenses,
    decimal? RentPricePerM2,
    decimal? AverageSalePriceUsd,
    int? SaleListings,
    decimal? SalePricePerM2Usd);

// Listings: every credit-eligible sale of the zone. Segments: by property type (apartment, house, ph, or all) and
// rooms (0 for every size, 1 to 5 with 5 meaning 5 or more), the ones with data.
public sealed record AdminZoneCreditResponse(int? Listings, IReadOnlyList<AdminCreditSegmentResponse> Segments);

// Prices in US dollars; null without a price distribution. Listings is null in data loaded before the counts.
public sealed record AdminCreditSegmentResponse(
    string PropertyType,
    int Rooms,
    int? Listings,
    decimal? MinimumPriceUsd,
    decimal? MedianPriceUsd,
    decimal? MaximumPriceUsd);
