namespace ZonaMatch.Domain.Scoring
{
    // Investment figures of a zone for the property type and size searched (ZoneProfile.GetMarket):
    // - gross yield: a year of rent (without expenses) over the sale price, in %; it needs the dollar rate the market
    //   was loaded with, since rents are in pesos and sale prices in dollars;
    // - payback: years of rent to pay the price back (100 / yield);
    // - price against its partido or comuna: how much cheaper (positive) or dearer (negative) the square meter is, in %;
    // - sale listings per 1,000 residents: how active the market is for reselling;
    // - households that rent (census): demand for a rental.
    // Each figure is null without the data it needs.
    public sealed record InvestmentMetrics(
        double? GrossYieldPercent,
        double? PaybackYears,
        double? PriceBelowParentPercent,
        string? ParentName,
        int? SaleListings,
        double? SaleListingsPerThousandResidents,
        double? RentingHouseholdsPercentage)
    {
        public static InvestmentMetrics For(ZoneProfile zone, ZoneProfile? parent, string? housingType, int? rooms, decimal? usdArs)
        {
            var market = zone.GetMarket(housingType, rooms);

            double? grossYield = market.AverageRent is { } rent && market.AverageSalePriceUsd is { } price && price > 0 && usdArs is > 0
                ? Math.Round((double)(rent * 12 / (price * usdArs.Value)) * 100, 2)
                : null;

            double? belowParent = parent is not null
                && market.SalePricePerM2Usd is { } pricePerM2 && pricePerM2 > 0
                && parent.GetMarket(housingType, rooms).SalePricePerM2Usd is { } parentPricePerM2 && parentPricePerM2 > 0
                ? Math.Round((1 - (double)(pricePerM2 / parentPricePerM2)) * 100, 1)
                : null;

            var listings = market.AvailablePropertiesForSale;
            double? listingsPerThousand = listings is { } count && zone.Population > 0
                ? Math.Round(count * 1000.0 / zone.Population, 2)
                : null;

            return new InvestmentMetrics(
                grossYield,
                grossYield is > 0 ? Math.Round(100 / grossYield.Value, 1) : null,
                belowParent,
                parent?.Name,
                listings,
                listingsPerThousand,
                zone.RentingHouseholdsPercentage);
        }
    }
}
