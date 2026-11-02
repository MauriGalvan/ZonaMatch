using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class ReviewTests
    {
        [Theory]
        [InlineData(3, 5, 4, 4, 2, 3.6)]
        [InlineData(5, 5, 5, 5, 5, 5.0)]
        [InlineData(1, 1, 1, 1, 2, 1.2)]
        public void OverallScoreIsTheAverageOfAllAspects(
            int safety, int transit, int connectivity, int commerce, int greenSpaces, double expected)
        {
            var review = new Review
            {
                SafetyScore = safety,
                TransitScore = transit,
                ConnectivityScore = connectivity,
                CommerceScore = commerce,
                GreenSpaceScore = greenSpaces
            };

            Assert.Equal(expected, review.OverallScore);
        }

        [Fact]
        public void OverallScoreUpdatesWhenAnAspectIsEdited()
        {
            var review = new Review
            {
                SafetyScore = 4,
                TransitScore = 4,
                ConnectivityScore = 4,
                CommerceScore = 4,
                GreenSpaceScore = 4
            };

            review.SafetyScore = 1;

            Assert.Equal(3.4, review.OverallScore);
        }
    }
}
