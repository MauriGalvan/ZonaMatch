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
    // values are inherited. ByType: apartment, house and ph, the ones with data.
    public sealed record MarketOperationResponse(
        int? Listings,
        int SampleSize,
        string Currency,
        decimal? AveragePrice,
        decimal? MedianPrice,
        decimal? AveragePriceM2,
        decimal? AverageExpensesArs,
        IReadOnlyList<MarketTypeResponse> ByType);

    public sealed record MarketTypeResponse(
        string PropertyType,
        int? Listings,
        int SampleSize,
        decimal? AveragePrice,
        decimal? AveragePriceM2);

    // Average rent and sale price per m2 (every type together) of the comuna or partido
    public sealed record MarketReferenceResponse(string Slug, string Name, decimal? AverageRent, decimal? AverageSalePriceM2);
}
