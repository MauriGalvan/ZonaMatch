using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class PriceDistributionTests
    {
        // 100 to 200 in steps of 10: every tenth of the listings costs 10 more
        private static readonly decimal[] Deciles = [100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200];

        [Theory]
        [InlineData(90, 0)]
        [InlineData(100, 0)]
        [InlineData(145, 45)]
        [InlineData(150, 50)]
        [InlineData(200, 100)]
        [InlineData(250, 100)]
        public void PercentageUpTo_ReturnsTheShareOfListingsWithinTheLimit(decimal limit, int expected)
        {
            Assert.Equal(expected, PriceDistribution.PercentageUpTo(Deciles, limit));
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
    }
}
