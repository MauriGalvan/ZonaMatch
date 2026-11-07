namespace ZonaMatch.Domain.Entities
{
    // Real estate market of a zone (table app.zone_market): one row per zone, operation and property type.
    // Loaded by the ETL, not by the API: etl/zonaprop/calculate_market.sql (Zonaprop listings) and
    // etl/estimate_rents.sql (estimated rent for zones still without real data).
    public class ZoneMarket
    {
        // Zone slug, as in GET /api/v1/zones/{slug}.
        public string ZoneSlug { get; set; } = string.Empty;

        // ZoneMarketOperation: sale or rent
        public string Operation { get; set; } = string.Empty;

        // ZoneMarketPropertyType: apartment, house, ph or all (every type together)
        public string PropertyType { get; set; } = string.Empty;

        // Active listings. Null when the values are inherited from a zone around this one.
        public int? Listings { get; set; }

        // Listings used for the averages (a sample of the most recent ones)
        public int SampleSize { get; set; }

        // Monthly rent without expenses, or sale price, in Currency (ARS for rent, USD for sale)
        public decimal? AveragePrice { get; set; }

        public decimal? MedianPrice { get; set; }

        public string Currency { get; set; } = string.Empty;

        // Price per covered m2 (total m2 when the covered one is missing), in Currency
        public decimal? AveragePriceM2 { get; set; }

        public decimal? MedianPriceM2 { get; set; }

        // Monthly expenses in pesos, of rentals and sales together
        public decimal? AverageExpensesArs { get; set; }

        // Typical surface of the listings (median), covered and total, in m2
        public decimal? MedianCoveredM2 { get; set; }

        public decimal? MedianTotalM2 { get; set; }

        // Shares (0 to 1) of the listings, only on the "all" row of each operation: eligible for a mortgage ("apto
        // credito", sale), published by the owner ("dueno directo") and accepting pets (rent)
        public decimal? CreditEligibleShare { get; set; }

        public decimal? OwnerShare { get; set; }

        public decimal? PetsAllowedShare { get; set; }

        // Price at every 5 % of the listings (21 points; older runs stored 11, one every tenth), from the cheapest to the
        // most expensive, without placeholder prices: the range and the distribution of the listings. Rent in pesos per month without expenses, sale in dollars.
        public decimal[]? PriceDeciles { get; set; }

        // The same per m2
        public decimal[]? PriceM2Deciles { get; set; }

        // Share of the listings (0 to 1) priced up to each point of PriceDeciles and PriceM2Deciles, ties included
        public decimal[]? PriceShares { get; set; }
        public decimal[]? PriceM2Shares { get; set; }

        // Sales of every type only: prices in dollars and the share (0 to 1) of the listings eligible for a mortgage
        // ("apto crédito") priced up to each one. Null without credit data (runs before 2026-11)
        public decimal[]? CreditPrices { get; set; }
        public decimal[]? CreditShares { get; set; }

        // ZoneMarketSource: where the values come from
        public string Source { get; set; } = string.Empty;

        // Slug of the zone from which values were inherited, when Source is zonaprop-inherited.
        public string? InheritedFrom { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    public static class ZoneMarketOperation
    {
        public const string Sale = "sale";
        public const string Rent = "rent";
    }

    public static class ZoneMarketPropertyType
    {
        public const string Apartment = "apartment";
        public const string House = "house";
        public const string Ph = "ph";
        public const string All = "all";
    }

    public static class ZoneMarketSource
    {
        // Measured from the zone's own Zonaprop listings
        public const string Zonaprop = "zonaprop";

        // The zone has no Zonaprop location: averages of the smallest measured zone around it
        public const string ZonapropInherited = "zonaprop-inherited";

        // For districts and communes, sum the values from the zones inside them.
        public const string ZonapropSum = "zonaprop-sum";

        // Rough rent from the census (etl/estimate_rents.sql), only where there is no real data
        public const string Estimated = "estimated";
    }
}
