using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class ReviewAspectTests
    {
        [Fact]
        public void TryNormalize_NormalizesCaseDuplicatesAndOrder()
        {
            var isValid = ReviewAspect.TryNormalize([" Transporte", "seguridad", "transporte", ""], out var topics, out _);

            Assert.True(isValid);
            Assert.Equal([ReviewAspect.Safety, ReviewAspect.PublicTransport], topics);
        }

        [Fact]
        public void TryNormalize_WhenNoTopicsAreProvided_ReturnsSuccess()
        {
            Assert.True(ReviewAspect.TryNormalize(null, out var topics, out _));
            Assert.Empty(topics);
        }

        [Fact]
        public void TryNormalize_WhenTopicIsUnknown_ReturnsItsName()
        {
            var isValid = ReviewAspect.TryNormalize(["seguridad", "ruido"], out var topics, out var error);

            Assert.False(isValid);
            Assert.Empty(topics);
            Assert.Contains("ruido", error);
        }
    }
}
