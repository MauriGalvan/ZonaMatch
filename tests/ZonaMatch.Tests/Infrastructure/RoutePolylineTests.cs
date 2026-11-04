using FluentAssertions;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public class RoutePolylineTests
{
    [Fact]
    public void GetLengthInMeters_WhenRouteHasMultipleSegments_SumsEverySegment()
    {
        // (0,0) -> (1,0) -> (1,1): measuring only the origin and destination would yield about 157 km.
        var lengthInMeters = RoutePolyline.GetLengthInMeters("??_ibE??_ibE", 5);

        lengthInMeters.Should().NotBeNull();
        lengthInMeters!.Value.Should().BeInRange(222_000, 222_500);
    }

    [Theory]
    [InlineData(5, 111_194.9266)]
    [InlineData(6, 11_119.4927)]
    [InlineData(7, 1_111.9493)]
    public void GetLengthInMeters_WhenPrecisionIsProvided_UsesMotisPrecision(int precision, double expected)
    {
        var lengthInMeters = RoutePolyline.GetLengthInMeters("??_ibE?", precision);

        lengthInMeters.Should().NotBeNull();
        lengthInMeters!.Value.Should().BeApproximately(expected, 0.0005);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("", 5)]
    [InlineData("_", 5)]
    [InlineData("??", 5)]
    [InlineData("??_ibE?", null)]
    [InlineData("??_ibE?", 8)]
    [InlineData("!", 5)]
    [InlineData("____________________", 5)]
    public void GetLengthInMeters_WhenGeometryIsInvalidOrIncomplete_ReturnsNoDistance(string? encodedPoints, int? precision)
    {
        RoutePolyline.GetLengthInMeters(encodedPoints, precision).Should().BeNull();
    }

    [Fact]
    public void GetLengthInMeters_WhenRoutePointsAreRepeated_ReturnsZeroDistance()
    {
        RoutePolyline.GetLengthInMeters("????", 5).Should().Be(0.0);
    }
}
