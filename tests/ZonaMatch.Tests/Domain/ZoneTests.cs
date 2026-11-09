using FluentAssertions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class ZoneTests
    {
        [Theory]
        [InlineData(-1224652, "r1224652")]
        [InlineData(45213, "w45213")]
        public void FeatureId_WhenOsmIdIsRelationOrWay_ReturnsOsmFeatureId(long osmId, string expected)
        {
            new Zone { OsmId = osmId }.FeatureId.Should().Be(expected);
        }
    }
}
