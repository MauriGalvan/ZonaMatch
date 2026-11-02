using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class PriceDistributionTests
    {
        // 100 to 200 in steps of 10: every tenth of the listings costs 10 more (older data, without shares)
        private static readonly decimal[] Deciles = [100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200];

        [Theory]
        [InlineData(90, 0)]
        [InlineData(100, 0)]
        [InlineData(145, 45)]
        [InlineData(150, 50)]
        [InlineData(200, 100)]
        [InlineData(250, 100)]
        public void PercentageUpTo_WithoutShares_SpacesThePointsEvenly(decimal limit, int expected)
        {
            Assert.Equal(expected, new PriceDistribution(Deciles).PercentageUpTo(limit));
        }

        // Like Ramos Mejía: many rentals at exactly $ 500.000, so 39 % cost up to it although the point is at 35 %
        private static readonly PriceDistribution RoundPrices = new(
            [250_000, 400_000, 490_000, 500_000, 520_000, 600_000, 1_000_000],
            [0.01m, 0.2m, 0.33m, 0.39m, 0.45m, 0.7m, 1]);

        [Theory]
        [InlineData(500_000, 39)]
        [InlineData(490_000, 33)]
        [InlineData(510_000, 42)]
        [InlineData(250_000, 1)]
        [InlineData(249_999, 0)]
        public void PercentageUpTo_WithShares_CountsEveryListingOnTheCap(decimal limit, int expected)
        {
            Assert.Equal(expected, RoundPrices.PercentageUpTo(limit));
        }

        [Fact]
        public void Scale_KeepsTheShares()
        {
            var monthly = RoundPrices.Scale(1, 100_000);

            Assert.Equal(600_000, monthly.Prices[3]);
            Assert.Equal(39, monthly.PercentageUpTo(600_000));
        }

        [Fact]
        public void From_WhenSharesDoNotMatchThePrices_SpacesThePointsEvenly()
        {
            var distribution = PriceDistribution.From(Deciles, [0.5m, 1])!;

            Assert.Null(distribution.Shares);
            Assert.Null(PriceDistribution.From([100], null));
        }

        [Fact]
        public void Calculate_WithDistribution_ComparesTheLimitWithTheRange()
        {
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageSalePriceUsd = 160_000, SaleDecilesUsd = [.. Deciles.Select(decile => decile * 1000)],
            };
            var engine = new ScoringEngine([zone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            // The average (160.000) is over the limit, but half of the listings fit: the limit is met and 10 points go
            var result = engine.Calculate(zone, new ScoringRequest(criteria, [], null, HousingOperations.Purchase, 150_000));
            var limit = result.Limits.Single();

            Assert.True(limit.IsSatisfied);
            Assert.Equal(40, result.Score);
            Assert.False(engine.Calculate(zone, new ScoringRequest(criteria, [], null, HousingOperations.Purchase, 90_000)).Limits.Single().IsSatisfied);
            Assert.Equal(
                "Precio: de US$ 100.000 a US$ 200.000, precio medio US$ 150.000: entra el 50 % de las publicaciones en tu tope de compra de US$ 150.000",
                limit.Details);
        }

        [Fact]
        public void Calculate_WithShares_UsesTheRealShareOfTheCap()
        {
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageRent = 520_000, RentDeciles = RoundPrices.Prices, RentShares = RoundPrices.Shares,
            };
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var limit = new ScoringEngine([zone]).Calculate(zone, new ScoringRequest(criteria, [], 500_000)).Limits.Single();

            Assert.EndsWith("entra el 39 % de las publicaciones en tu tope de $ 500.000", limit.Details);
        }
    }
}
