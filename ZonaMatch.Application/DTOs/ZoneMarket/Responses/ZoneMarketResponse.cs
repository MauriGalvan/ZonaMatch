namespace ZonaMatch.Application.DTOs.ZoneMarket.Responses
{
    // GET /api/v1/zones/{slug}/market: what the listings say about a zone, summarized.
    // Source: zonaprop (listings of the zone), zonaprop-inherited (the zone has no listings of its own: values of
    // InheritedFromName), zonaprop-sum (comuna or partido: its zones added up) or estimated (rent estimated from the
    // census, no listings). Reference: the same numbers for the comuna or partido around it, to compare.
    public sealed record ZoneMarketResponse(
        string Slug,
        string Source,
        string? InheritedFromName,
        DateTimeOffset UpdatedAt,
        MarketOperationResponse? Rent,
        MarketOperationResponse? Sale,
        MarketReferenceResponse? Reference);

    // One operation (rent or sale). Prices in Currency: ARS per month for rent, USD for sale. Listings is null when the
    // values are inherited. MedianCoveredM2 and MedianTotalM2: typical surface. Shares (0 to 1) of the listings:
    // eligible for a mortgage (sale), published by the owner, accepting pets (rent); null when unknown.
    // ByType: apartment, house and ph, the ones with data. ByRooms: 1 to 5 rooms (5 = 5 or more), the ones with data.
    public sealed record MarketOperationResponse(
        int? Listings,
        int SampleSize,
        string Currency,
        decimal? AveragePrice,
        decimal? MedianPrice,
        decimal? AveragePriceM2,
        decimal? AverageExpensesArs,
        IReadOnlyList<MarketTypeResponse> ByType,
        decimal? MedianCoveredM2 = null,
        decimal? MedianTotalM2 = null,
        decimal? CreditEligibleShare = null,
        decimal? OwnerShare = null,
        decimal? PetsAllowedShare = null,
        IReadOnlyList<MarketRoomsResponse>? ByRooms = null);

    public sealed record MarketTypeResponse(
        string PropertyType,
        int? Listings,
        int SampleSize,
        decimal? AveragePrice,
        decimal? AveragePriceM2,
        decimal? MedianCoveredM2 = null,
        IReadOnlyList<MarketRoomsResponse>? ByRooms = null);

    // Average rent and sale price per m2 (every type together) of the comuna or partido
    public sealed record MarketReferenceResponse(string Slug, string Name, decimal? AverageRent, decimal? AverageSalePriceM2);
}
