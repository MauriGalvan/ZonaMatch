using ZonaMatch.Application.DTOs.ZoneMarket.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zonas
{
    // Real estate market of a zone: rent and sale, every type together, by type and by number of rooms, how many
    // listings, which share is eligible for a mortgage, published by the owner or accepting pets, where the data comes
    // from, and the comuna or partido around it to compare.
    public class GetZoneMarket(IZoneMarketRepository market)
    {
        private static readonly string[] TypeOrder =
            [ZoneMarketPropertyType.Apartment, ZoneMarketPropertyType.House, ZoneMarketPropertyType.Ph];

        public async Task<ZoneMarketResponse> ExecuteAsync(string slug, CancellationToken cancellationToken = default)
        {
            var rows = await market.ListByZoneAsync(slug, cancellationToken);

            // No listings nor estimate loaded for the zone (MERCADO-ZONAS-DOCKER.bat has not run for it)
            if (rows.Count == 0)
                throw new NotFoundException("Todavía no hay datos del mercado inmobiliario para esta zona.");

            var rooms = await market.ListRoomsByZoneAsync(slug, cancellationToken);
            var main = rows.FirstOrDefault(row => row.PropertyType == ZoneMarketPropertyType.All) ?? rows[0];
            var inheritedName = main.InheritedFrom is { } from ? await market.GetZoneNameAsync(from, cancellationToken) : null;

            return new ZoneMarketResponse(
                slug,
                main.Source,
                inheritedName,
                rows.Max(row => row.UpdatedAt),
                Operation(rows, rooms, ZoneMarketOperation.Rent),
                Operation(rows, rooms, ZoneMarketOperation.Sale),
                await ReferenceAsync(slug, cancellationToken));
        }

        private static MarketOperationResponse? Operation(
            IReadOnlyList<ZoneMarket> rows, IReadOnlyList<ZoneMarketRooms> rooms, string operation)
        {
            var ofOperation = rows.Where(row => row.Operation == operation).ToList();
            var all = ofOperation.FirstOrDefault(row => row.PropertyType == ZoneMarketPropertyType.All);
            if (all is null || all.AveragePrice is null)
                return null;

            return new MarketOperationResponse(
                all.Listings,
                all.SampleSize,
                all.Currency,
                all.AveragePrice,
                all.MedianPrice,
                all.AveragePriceM2,
                all.AverageExpensesArs,
                TypeOrder
                    .Select(type => ofOperation.FirstOrDefault(row => row.PropertyType == type))
                    .OfType<ZoneMarket>()
                    .Where(row => row.AveragePrice is not null)
                    .Select(row => new MarketTypeResponse(
                        row.PropertyType, row.Listings, row.SampleSize, row.AveragePrice, row.AveragePriceM2, row.MedianCoveredM2,
                        ByRooms(rooms, operation, row.PropertyType)))
                    .ToList(),
                all.MedianCoveredM2,
                all.MedianTotalM2,
                all.CreditEligibleShare,
                all.OwnerShare,
                all.PetsAllowedShare,
                ByRooms(rooms, operation, ZoneMarketPropertyType.All));
        }

        // Room groups with a price and enough listings, from 1 room to 5 or more
        private static IReadOnlyList<MarketRoomsResponse> ByRooms(
            IReadOnlyList<ZoneMarketRooms> rooms, string operation, string propertyType) =>
            rooms
                .Where(row => row.Operation == operation && row.PropertyType == propertyType)
                .Where(row => row.AveragePrice is not null && row.SampleSize >= ZoneMarketRooms.MinSampleSize)
                .OrderBy(row => row.Rooms)
                .Select(row => new MarketRoomsResponse(
                    row.Rooms, row.Listings, row.SampleSize, row.AveragePrice, row.MedianPrice, row.AveragePriceM2,
                    row.AverageExpensesArs, row.MedianCoveredM2))
                .ToList();

        private async Task<MarketReferenceResponse?> ReferenceAsync(string slug, CancellationToken cancellationToken)
        {
            if (await market.GetParentAsync(slug, cancellationToken) is not { } parent)
                return null;

            var rows = await market.ListByZoneAsync(parent.Slug, cancellationToken);
            decimal? Value(string operation, Func<ZoneMarket, decimal?> pick) =>
                rows.FirstOrDefault(row => row.Operation == operation && row.PropertyType == ZoneMarketPropertyType.All) is { } row
                    ? pick(row)
                    : null;

            var rent = Value(ZoneMarketOperation.Rent, row => row.AveragePrice);
            var saleM2 = Value(ZoneMarketOperation.Sale, row => row.AveragePriceM2);
            return rent is null && saleM2 is null ? null : new MarketReferenceResponse(parent.Slug, parent.Name, rent, saleM2);
        }
    }
}
