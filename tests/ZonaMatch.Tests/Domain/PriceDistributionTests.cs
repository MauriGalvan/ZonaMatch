using FluentAssertions;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class PriceDistributionTests
    {
        // 100 to 200 in steps of 10: every tenth of the listings costs 10 more (older data, without shares)
        private static readonly decimal[] Deciles = [100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200];

        // Like Ramos Mejía: many rentals at exactly $ 500.000, so 39 % cost up to it although the point is at 35 %
        private static readonly PriceDistribution RoundPrices = new(
            [250_000, 400_000, 490_000, 500_000, 520_000, 600_000, 1_000_000],
            [0.01m, 0.2m, 0.33m, 0.39m, 0.45m, 0.7m, 1]);

        [Theory]
        [InlineData(90, 0)]
        [InlineData(100, 0)]
        [InlineData(145, 45)]
        [InlineData(150, 50)]
        [InlineData(200, 100)]
        [InlineData(250, 100)]
        public void PercentageUpTo_WhenThereAreNoShares_SpacesThePointsEvenly(decimal limit, int expected)
        {
            var distribution = new PriceDistribution(Deciles);

            var percentage = distribution.PercentageUpTo(limit);

            percentage.Should().Be(expected);
        }

        [Theory]
        [InlineData(500_000, 39)]
        [InlineData(490_000, 33)]
        [InlineData(510_000, 42)]
        [InlineData(250_000, 1)]
        [InlineData(249_999, 0)]
        public void PercentageUpTo_WhenThereAreShares_CountsEveryListingOnTheCap(decimal limit, int expected)
        {
            var percentage = RoundPrices.PercentageUpTo(limit);

            percentage.Should().Be(expected);
        }

        [Theory]
        [InlineData(95_000)]
        [InlineData(300_000)]
        public void PercentageUpTo_WhenThePointsStopBeforeTheMostExpensive_KeepsTheLastShare(decimal limit)
        {
            // Credit-eligible sales read up to US$ 95.000 only: the 18 % above it is not known to fit
            var distribution = new PriceDistribution([0, 43_000, 95_000], [0, 0.052m, 0.817m]);

            var percentage = distribution.PercentageUpTo(limit);

            percentage.Should().Be(82);
        }

        [Fact]
        public void Scale_WhenPricesChange_KeepsTheShares()
        {
            var monthly = RoundPrices.Scale(1, 100_000);

            monthly.Prices[3].Should().Be(600_000);
            monthly.PercentageUpTo(600_000).Should().Be(39);
        }

        [Fact]
        public void From_WhenSharesDoNotMatchThePrices_SpacesThePointsEvenly()
        {
            var distribution = PriceDistribution.From(Deciles, [0.5m, 1]);

            distribution.Should().NotBeNull();
            distribution!.Shares.Should().BeNull();
        }

        [Fact]
        public void From_WhenThereIsASinglePrice_ReturnsNull()
        {
            var distribution = PriceDistribution.From([100], null);

            distribution.Should().BeNull();
        }
    }
}
