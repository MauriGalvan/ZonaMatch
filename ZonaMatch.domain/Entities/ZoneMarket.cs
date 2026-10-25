namespace ZonaMatch.Domain.Entities
{
    // Real estate market of a zone (table app.zone_market): one row per zone, operation and property type.
    // Loaded by the ETL, not by the API: etl/zonaprop/calculate_market.sql (Zonaprop listings) and
    // etl/estimar_alquileres.sql (estimated rent for zones still without real data).
    public class ZoneMarket
    {
        // Zone slug, the same as in GET /Zonas/{slug}
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

        // Monthly expenses of rentals, in pesos
        public decimal? AverageExpensesArs { get; set; }

        // ZoneMarketSource: where the values come from
        public string Source { get; set; } = string.Empty;

        // Slug of the zone the values were taken from, when Source is zonaprop-inherited
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

        // Partidos and comunas: sum of the zones inside them
        public const string ZonapropSum = "zonaprop-sum";

        // Rough rent from the census (etl/estimar_alquileres.sql), only where there is no real data
        public const string Estimated = "estimated";
    }
}
