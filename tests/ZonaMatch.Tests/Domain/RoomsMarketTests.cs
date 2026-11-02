using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    // Prices by number of rooms (app.zone_market_rooms) in the zone profile and in the score limits
    public class RoomsMarketTests
    {
        private static readonly Dictionary<string, string> Criteria = new() { [ScoringEngine.Education] = "media" };

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
            var market = Zone().GetMarket(HousingTypes.Apartment, 2);

            Assert.Equal(1_229_000, market.AverageRent);
            Assert.Equal(250_000, market.AverageExpenses);
            Assert.Equal(170_547, market.AverageSalePriceUsd);
            Assert.Equal(2, market.RentRooms);
            Assert.Equal(2, market.SaleRooms);
        }

        [Fact]
        public void GetMarket_WhenOneOperationLacksRoomsData_KeepsTheAverageOfEverySizeForIt()
        {
            // 5 or more rooms: rent of its own, no sale data; expenses fall back to the type's
            var market = Zone().GetMarket(HousingTypes.Apartment, 7);

            Assert.Equal(5_097_000, market.AverageRent);
            Assert.Equal(5, market.RentRooms);
            Assert.Equal(291_000, market.AverageExpenses);
            Assert.Equal(258_200, market.AverageSalePriceUsd);
            Assert.Null(market.SaleRooms);
        }

        [Fact]
        public void GetMarket_WhenSizeIsMissing_UsesTheAverageOfEverySize()
        {
            var market = Zone().GetMarket(HousingTypes.Apartment, 3);

            Assert.Equal(1_289_000, market.AverageRent);
            Assert.Null(market.RentRooms);
        }

        [Fact]
        public void GetMarket_WithoutPropertyType_UsesTheRoomsOfEveryType()
        {
            var market = Zone().GetMarket(null, 1);

            Assert.Equal(872_000, market.AverageRent);
            Assert.Equal(1, market.RentRooms);
        }

        [Fact]
        public void Calculate_WithRooms_ComparesTheBudgetWithThatSize()
        {
            var zone = Zone();
            var limit = new ScoringEngine([zone]).Calculate(zone, new ScoringRequest(
                Criteria, [], 1_500_000, HousingType: HousingTypes.Apartment, Rooms: 2)).Limits.Single();

            // 1.229.000 of rent + 250.000 of expenses
            Assert.True(limit.IsSatisfied);
            Assert.Equal(
                "Alquiler de departamentos de 2 ambientes $ 1.229.000 + expensas $ 250.000 = $ 1.479.000 por mes: dentro de tu tope de $ 1.500.000",
                limit.Details);
        }

        [Fact]
        public void Calculate_WithRoomsAndSurface_EstimatesWithThePricePerM2OfThatSize()
        {
            var zone = Zone();
            var limit = new ScoringEngine([zone]).Calculate(zone, new ScoringRequest(
                Criteria, [], null, HousingOperations.Purchase, 150_000, HousingTypes.Apartment, AreaM2: 40, Rooms: 2))
                .Limits.Single();

            // 3.738 x 40 = 149.520
            Assert.True(limit.IsSatisfied);
            Assert.StartsWith("Precio estimado de departamentos de 2 ambientes para 40 m² US$ 149.520 (US$ 3.738/m²)", limit.Details);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData(1, " de 1 ambiente")]
        [InlineData(3, " de 3 ambientes")]
        [InlineData(5, " de 5 ambientes o más")]
        public void TextSuffix_NamesTheRoomGroup(int? rooms, string expected)
        {
            Assert.Equal(expected, HousingRooms.TextSuffix(rooms));
        }
    }
}
