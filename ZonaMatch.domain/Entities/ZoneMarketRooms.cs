namespace ZonaMatch.Domain.Entities
{
    // Real estate market of a zone for one room group (table app.zone_market_rooms): one row per zone, operation,
    // property type and number of rooms. Loaded by the ETL (etl/zonaprop/calculate_market.sql) like ZoneMarket, whose
    // rows are the same figures for every room count.
    public class ZoneMarketRooms
    {
        public const int MaxRooms = 5;

        // Room groups with fewer sampled listings are too noisy to show or to price with
        public const int MinSampleSize = 5;

        // Zone slug, the same as in GET /Zonas/{slug}
        public string ZoneSlug { get; set; } = string.Empty;

        // ZoneMarketOperation: sale or rent
        public string Operation { get; set; } = string.Empty;

        // ZoneMarketPropertyType: apartment, house, ph or all (every type together)
        public string PropertyType { get; set; } = string.Empty;

        // 1 to 4, and 5 for 5 rooms or more
        public int Rooms { get; set; }

        // Active listings with that number of rooms. Null when the values are inherited from a zone around this one.
        public int? Listings { get; set; }

        // Listings used for the averages
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

        // Typical surface (median), covered and total, in m2
        public decimal? MedianCoveredM2 { get; set; }

        public decimal? MedianTotalM2 { get; set; }

        // ZoneMarketSource: zonaprop, zonaprop-inherited or zonaprop-sum
        public string Source { get; set; } = string.Empty;

        // Slug of the zone the values were taken from, when Source is zonaprop-inherited
        public string? InheritedFrom { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
