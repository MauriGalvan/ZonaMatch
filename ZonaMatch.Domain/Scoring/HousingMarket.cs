namespace ZonaMatch.Domain.Scoring
{
    // Real estate market data for a zone and housing type (Zonaprop): rent and building expenses in Argentine pesos,
    // sale prices in US dollars, and prices per square meter. Null values indicate unavailable data.
    public sealed record HousingMarket(
        decimal? AverageRent,
        int? AvailablePropertiesForRent,
        decimal? AverageExpenses,
        decimal? AverageSalePriceUsd,
        int? AvailablePropertiesForSale,
        decimal? RentPricePerM2 = null,
        decimal? SalePricePerM2Usd = null)
    {
        public static readonly HousingMarket NoData = new(null, null, null, null, null);

        // Monthly rent cost: rent plus building expenses, or rent alone when expenses are unavailable.
        public decimal? MonthlyRentCost => AverageRent is { } rent ? rent + (AverageExpenses ?? 0) : null;

        // When area and price per square meter are available, calculate the price for that area; otherwise use the zone
        // average. The average includes all sizes, and a small one-bedroom apartment usually costs much less.
        public bool EstimatesRent(int? areaM2) => areaM2 is > 0 && RentPricePerM2 is not null;

        public bool EstimatesSale(int? areaM2) => areaM2 is > 0 && SalePricePerM2Usd is not null;

        public decimal? RentForArea(int? areaM2) =>
            EstimatesRent(areaM2) ? Math.Round(RentPricePerM2!.Value * areaM2!.Value) : AverageRent;

        public decimal? SalePriceUsdForArea(int? areaM2) =>
            EstimatesSale(areaM2) ? Math.Round(SalePricePerM2Usd!.Value * areaM2!.Value) : AverageSalePriceUsd;

        public decimal? MonthlyRentCostForArea(int? areaM2) =>
            RentForArea(areaM2) is { } rent ? rent + (AverageExpenses ?? 0) : null;
    }

    // The user's housing operation: rent or purchase.
    public static class HousingOperations
    {
        public const string Rent = "alquiler";
        public const string Purchase = "compra";

        public static bool IsValid(string? operation) => operation is Rent or Purchase;
    }

    // Housing types using the same values as app.zone_market.property_type ("all" represents the combined market).
    public static class HousingTypes
    {
        public const string Apartment = "apartment";
        public const string House = "house";
        public const string Ph = "ph";
        public const string All = "all";

        public static bool IsValid(string? type) => type is Apartment or House or Ph;

        // Only apartments include building expenses; houses and PHs do not.
        public static bool IncludesBuildingExpenses(string? type) => type is Apartment;

        // Text suffixes such as " of apartments"; empty when no type is specified.
        public static string TextSuffix(string? type) => type switch
        {
            Apartment => " de departamentos",
            House => " de casas",
            Ph => " de PH",
            _ => string.Empty,
        };
    }
}
