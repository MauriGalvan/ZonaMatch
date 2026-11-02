using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public class RoutePolylineTests
{
    [Fact]
    public void MeasuresEveryRouteSegment()
    {
        // (0,0) -> (1,0) -> (1,1): measuring only the origin and destination would yield about 157 km.
        var lengthInMeters = RoutePolyline.GetLengthInMeters("??_ibE??_ibE", 5);

        Assert.NotNull(lengthInMeters);
        Assert.InRange(lengthInMeters.Value, 222_000, 222_500);
    }

    [Theory]
    [InlineData(5, 111_194.9266)]
    [InlineData(6, 11_119.4927)]
    [InlineData(7, 1_111.9493)]
    public void UsesThePrecisionReportedByMotis(int precision, double expected)
    {
        var lengthInMeters = RoutePolyline.GetLengthInMeters("??_ibE?", precision);

        Assert.NotNull(lengthInMeters);
        Assert.Equal(expected, lengthInMeters.Value, 3);
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
    public void InvalidOrIncompleteGeometryReturnsNoDistance(string? encodedPoints, int? precision)
    {
        Assert.Null(RoutePolyline.GetLengthInMeters(encodedPoints, precision));
    }

    [Fact]
    public void RepeatedPointsHaveZeroDistance()
    {
        Assert.Equal(0.0, RoutePolyline.GetLengthInMeters("????", 5));
    }
}
