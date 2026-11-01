namespace ZonaMatch.Application.DTOs.ZoneMarket.Responses
{
    // Market of one room group (1 to 4, 5 = 5 or more) for an operation and property type, in the operation's currency.
    // Listings is null when the values are inherited.
    public sealed record MarketRoomsResponse(
        int Rooms,
        int? Listings,
        int SampleSize,
        decimal? AveragePrice,
        decimal? MedianPrice,
        decimal? AveragePriceM2,
        decimal? AverageExpensesArs,
        decimal? MedianCoveredM2);
}
