namespace ZonaMatch.Domain.Scoring
{
    // Real estate market data for a zone and housing type (Zonaprop): rent and building expenses in Argentine pesos,
    // sale prices in US dollars, and prices per square meter. Null values indicate unavailable data. RentRooms and
    // SaleRooms: the number of rooms the prices are for (1 to 5, 5 = 5 or more); null when they cover every size.
    // The deciles are the price distribution (PriceDistribution): rent per month without expenses, sale in dollars.
    public sealed record HousingMarket(
        decimal? AverageRent,
        int? AvailablePropertiesForRent,
        decimal? AverageExpenses,
        decimal? AverageSalePriceUsd,
        int? AvailablePropertiesForSale,
        decimal? RentPricePerM2 = null,
        decimal? SalePricePerM2Usd = null,
        int? RentRooms = null,
        int? SaleRooms = null,
        decimal[]? RentDeciles = null,
        decimal[]? RentPerM2Deciles = null,
        decimal[]? SaleDecilesUsd = null,
        decimal[]? SalePerM2DecilesUsd = null)
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

        // Distribution of what renting costs per month (rent plus building expenses), for the area when there is a
        // price per square meter; null without a distribution.
        public decimal[]? MonthlyRentDistribution(int? areaM2)
        {
            var expenses = AverageExpenses ?? 0;
            if (areaM2 is > 0 && PriceDistribution.IsValid(RentPerM2Deciles))
                return PriceDistribution.Scale(RentPerM2Deciles!, areaM2.Value, expenses);

            return !EstimatesRent(areaM2) && PriceDistribution.IsValid(RentDeciles)
                ? PriceDistribution.Scale(RentDeciles!, 1, expenses)
                : null;
        }

        // Distribution of the sale price in US dollars, for the area when there is a price per square meter; null
        // without a distribution.
        public decimal[]? SalePriceDistributionUsd(int? areaM2)
        {
            if (areaM2 is > 0 && PriceDistribution.IsValid(SalePerM2DecilesUsd))
                return PriceDistribution.Scale(SalePerM2DecilesUsd!, areaM2.Value);

            return !EstimatesSale(areaM2) && PriceDistribution.IsValid(SaleDecilesUsd) ? SaleDecilesUsd : null;
        }
    }

    // The user's housing operation: rent or purchase.
    public static class HousingOperations
    {
        public const string Rent = "alquiler";
        public const string Purchase = "compra";

        public static bool IsValid(string? operation) => operation is Rent or Purchase;
    }

    // Number of rooms the user is looking for, with the groups of app.zone_market_rooms: 1 to 4, and 5 for 5 or more.
    public static class HousingRooms
    {
        public const int Minimum = 1;
        public const int Maximum = 5;

        public static bool IsValid(int rooms) => rooms is >= Minimum and <= Maximum;

        // Text suffixes such as " de 2 ambientes"; empty when no number of rooms is specified.
        public static string TextSuffix(int? rooms) => rooms switch
        {
            null => string.Empty,
            1 => " de 1 ambiente",
            >= Maximum => $" de {Maximum} ambientes o más",
            _ => $" de {rooms} ambientes",
        };
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
