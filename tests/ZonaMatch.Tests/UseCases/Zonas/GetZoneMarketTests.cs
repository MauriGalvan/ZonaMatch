using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zonas;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Zonas
{
    public class GetZoneMarketTests
    {
        private readonly FakeZoneMarketRepository _market = new();

        private static ZoneMarket Row(string slug, string operation, string type, decimal price, decimal? m2 = null,
            int? listings = 100, string source = ZoneMarketSource.Zonaprop, string? inheritedFrom = null) => new()
        {
            ZoneSlug = slug,
            Operation = operation,
            PropertyType = type,
            Listings = listings,
            SampleSize = 60,
            AveragePrice = price,
            AveragePriceM2 = m2,
            Currency = operation == ZoneMarketOperation.Rent ? "ARS" : "USD",
            Source = source,
            InheritedFrom = inheritedFrom,
            UpdatedAt = new DateTimeOffset(2026, 10, 24, 23, 0, 0, TimeSpan.Zero),
        };

        [Fact]
        public async Task ExecuteAsync_WhenZoneHasListings_SummarizesRentAndSaleByTypeAndComparesWithItsPartido()
        {
            _market.Rows.AddRange([
                Row("castelar", ZoneMarketOperation.Rent, ZoneMarketPropertyType.All, 861_000, 14_891, 240),
                Row("castelar", ZoneMarketOperation.Rent, ZoneMarketPropertyType.Apartment, 700_000, 15_000, 180),
                Row("castelar", ZoneMarketOperation.Rent, ZoneMarketPropertyType.House, 1_200_000, 9_000, 60),
                Row("castelar", ZoneMarketOperation.Sale, ZoneMarketPropertyType.All, 139_686, 1_458, 3_074),
                Row("partido-de-moron", ZoneMarketOperation.Rent, ZoneMarketPropertyType.All, 700_000, null, 900, ZoneMarketSource.ZonapropSum),
                Row("partido-de-moron", ZoneMarketOperation.Sale, ZoneMarketPropertyType.All, 120_000, 1_400, 9_000, ZoneMarketSource.ZonapropSum),
            ]);
            _market.Parent = ("partido-de-moron", "Partido de Morón");

            var market = await new GetZoneMarket(_market).ExecuteAsync("castelar");

            Assert.Equal(ZoneMarketSource.Zonaprop, market.Source);
            Assert.Equal(861_000, market.Rent!.AveragePrice);
            Assert.Equal(240, market.Rent.Listings);
            Assert.Equal(["apartment", "house"], market.Rent.ByType.Select(type => type.PropertyType));
            Assert.Equal(1_458, market.Sale!.AveragePriceM2);
            Assert.Equal("Partido de Morón", market.Reference!.Name);
            Assert.Equal(700_000, market.Reference.AverageRent);
            Assert.Equal(1_400, market.Reference.AverageSalePriceM2);
        }

        private static ZoneMarketRooms RoomsRow(string operation, string type, int rooms, decimal price, int sampleSize = 30) => new()
        {
            ZoneSlug = "palermo",
            Operation = operation,
            PropertyType = type,
            Rooms = rooms,
            Listings = 1_000,
            SampleSize = sampleSize,
            AveragePrice = price,
            MedianCoveredM2 = 30 + rooms * 15,
            Currency = operation == ZoneMarketOperation.Rent ? "ARS" : "USD",
            Source = ZoneMarketSource.Zonaprop,
            UpdatedAt = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero),
        };

        [Fact]
        public async Task ExecuteAsync_WhenZoneHasFiltersAndRooms_ReturnsSharesSurfaceAndPricesByRooms()
        {
            var sale = Row("palermo", ZoneMarketOperation.Sale, ZoneMarketPropertyType.All, 274_330, 3_865, 12_585);
            sale.CreditEligibleShare = 0.2578m;
            sale.OwnerShare = 0.0132m;
            sale.MedianCoveredM2 = 58;
            sale.MedianTotalM2 = 71;
            var rent = Row("palermo", ZoneMarketOperation.Rent, ZoneMarketPropertyType.All, 1_351_000, 25_298, 3_535);
            rent.PetsAllowedShare = 0.2571m;
            _market.Rows.AddRange([sale, rent,
                Row("palermo", ZoneMarketOperation.Sale, ZoneMarketPropertyType.Apartment, 258_200, 3_910, 11_967)]);
            _market.RoomsRows.AddRange([
                RoomsRow(ZoneMarketOperation.Sale, ZoneMarketPropertyType.All, 2, 170_148),
                RoomsRow(ZoneMarketOperation.Sale, ZoneMarketPropertyType.All, 1, 120_733),
                // Too few listings to show
                RoomsRow(ZoneMarketOperation.Sale, ZoneMarketPropertyType.All, 5, 1_069_191, sampleSize: 3),
                RoomsRow(ZoneMarketOperation.Sale, ZoneMarketPropertyType.Apartment, 2, 170_547),
                RoomsRow(ZoneMarketOperation.Rent, ZoneMarketPropertyType.All, 2, 1_227_000),
            ]);

            var market = await new GetZoneMarket(_market).ExecuteAsync("palermo");

            Assert.Equal(0.2578m, market.Sale!.CreditEligibleShare);
            Assert.Equal(0.0132m, market.Sale.OwnerShare);
            Assert.Equal(58, market.Sale.MedianCoveredM2);
            Assert.Equal(71, market.Sale.MedianTotalM2);
            Assert.Equal(0.2571m, market.Rent!.PetsAllowedShare);
            Assert.Equal([1, 2], market.Sale.ByRooms!.Select(rooms => rooms.Rooms));
            Assert.Equal(170_148, market.Sale.ByRooms![1].AveragePrice);
            Assert.Equal(170_547, market.Sale.ByType.Single().ByRooms!.Single().AveragePrice);
            Assert.Equal(1_227_000, market.Rent.ByRooms!.Single().AveragePrice);
        }

        [Fact]
        public async Task ExecuteAsync_WhenValuesAreInherited_NamesTheZoneTheyComeFrom()
        {
            _market.Rows.Add(Row("barrio-river", ZoneMarketOperation.Rent, ZoneMarketPropertyType.All, 1_432_000, listings: null,
                source: ZoneMarketSource.ZonapropInherited, inheritedFrom: "belgrano"));
            _market.Names["belgrano"] = "Belgrano";

            var market = await new GetZoneMarket(_market).ExecuteAsync("barrio-river");

            Assert.Equal("Belgrano", market.InheritedFromName);
            Assert.Null(market.Rent!.Listings);
            Assert.Null(market.Sale);
        }

        [Fact]
        public async Task ExecuteAsync_WhenZoneHasNoData_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => new GetZoneMarket(_market).ExecuteAsync("no-existe"));
        }

        private sealed class FakeZoneMarketRepository : IZoneMarketRepository
        {
            public List<ZoneMarket> Rows { get; } = [];
            public List<ZoneMarketRooms> RoomsRows { get; } = [];
            public (string Slug, string Name)? Parent { get; set; }
            public Dictionary<string, string> Names { get; } = [];

            public Task<IReadOnlyList<ZoneMarket>> ListByZoneAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ZoneMarket>>(Rows.Where(row => row.ZoneSlug == slug).ToList());

            public Task<IReadOnlyList<ZoneMarketRooms>> ListRoomsByZoneAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ZoneMarketRooms>>(RoomsRows.Where(row => row.ZoneSlug == slug).ToList());

            public Task<(string Slug, string Name)?> GetParentAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult(Parent);

            public Task<string?> GetZoneNameAsync(string slug, CancellationToken cancellationToken = default) =>
                Task.FromResult(Names.GetValueOrDefault(slug));
        }
    }
}
