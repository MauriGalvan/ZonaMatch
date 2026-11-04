using FluentAssertions;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    // Prices by number of rooms (app.zone_market_rooms) in the zone profile
    public class ZoneProfileTests
    {
        // Like Palermo: apartments for rent average $ 1.289.000, but a 2-room one is about $ 1.229.000
        private static ZoneProfile Zone() => new()
        {
            Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
            AverageExpenses = 280_000,
            MarketByPropertyType = new Dictionary<string, HousingMarket>
            {
                [HousingTypes.Apartment] = new(1_289_000, 3_413, 291_000, 258_200, 11_967, RentPricePerM2: 25_259, SalePricePerM2Usd: 3_910),
            },
            MarketByRooms = new Dictionary<(string Type, int Rooms), HousingMarket>
            {
                [(HousingTypes.Apartment, 2)] = new(1_229_000, 1_353, 250_000, 170_547, 3_560, RentPricePerM2: 25_523, SalePricePerM2Usd: 3_738),
                [(HousingTypes.Apartment, 5)] = new(5_097_000, 104, null, null, null, RentPricePerM2: 28_599),
                [(HousingTypes.All, 1)] = new(872_000, 1_029, null, 120_733, 2_785),
            },
        };

        [Fact]
        public void GetMarket_WhenRoomsHaveData_UsesThePricesOfThatSize()
        {
            var zone = Zone();

            var market = zone.GetMarket(HousingTypes.Apartment, 2);

            market.AverageRent.Should().Be(1_229_000);
            market.AverageExpenses.Should().Be(250_000);
            market.AverageSalePriceUsd.Should().Be(170_547);
            market.RentRooms.Should().Be(2);
            market.SaleRooms.Should().Be(2);
        }

        [Fact]
        public void GetMarket_WhenOneOperationLacksRoomsData_KeepsTheAverageOfEverySizeForIt()
        {
            var zone = Zone();

            // 5 or more rooms: rent of its own, no sale data; expenses fall back to the type's
            var market = zone.GetMarket(HousingTypes.Apartment, 7);

            market.AverageRent.Should().Be(5_097_000);
            market.RentRooms.Should().Be(5);
            market.AverageExpenses.Should().Be(291_000);
            market.AverageSalePriceUsd.Should().Be(258_200);
            market.SaleRooms.Should().BeNull();
        }

        [Fact]
        public void GetMarket_WhenSizeIsMissing_UsesTheAverageOfEverySize()
        {
            var zone = Zone();

            var market = zone.GetMarket(HousingTypes.Apartment, 3);

            market.AverageRent.Should().Be(1_289_000);
            market.RentRooms.Should().BeNull();
        }

        [Fact]
        public void GetMarket_WhenPropertyTypeIsMissing_UsesTheRoomsOfEveryType()
        {
            var zone = Zone();

            var market = zone.GetMarket(null, 1);

            market.AverageRent.Should().Be(872_000);
            market.RentRooms.Should().Be(1);
        }
    }
}
