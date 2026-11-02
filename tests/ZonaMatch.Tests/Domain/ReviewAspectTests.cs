using FluentAssertions;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class ReviewAspectTests
    {
        [Fact]
        public void TryNormalize_WhenTopicsHaveDifferentCaseDuplicatesAndOrder_NormalizesTopics()
        {
            var isValid = ReviewAspect.TryNormalize([" Transporte", "seguridad", "transporte", ""], out var topics, out _);

            isValid.Should().BeTrue();
            topics.Should().Equal(ReviewAspect.Safety, ReviewAspect.PublicTransport);
        }

        [Fact]
        public void TryNormalize_WhenNoTopicsAreProvided_ReturnsSuccess()
        {
            ReviewAspect.TryNormalize(null, out var topics, out _).Should().BeTrue();
            topics.Should().BeEmpty();
        }

        [Fact]
        public void TryNormalize_WhenTopicIsUnknown_ReturnsItsName()
        {
            var isValid = ReviewAspect.TryNormalize(["seguridad", "ruido"], out var topics, out var error);

            isValid.Should().BeFalse();
            topics.Should().BeEmpty();
            error.Should().Contain("ruido");
        }
    }
}
