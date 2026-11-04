using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Map;

namespace ZonaMatch.Tests.UseCases.Map;

public class GetMapDistrictsTests
{
    [Fact]
    public async Task GetMapDistricts_WhenToleranceIsMissing_UsesDefaultTolerance()
    {
        var repository = new Mock<IMapRepository>();
        repository.Setup(item => item.GetDistrictsAsync(0.0005)).ReturnsAsync("districts-geojson");

        var result = await new GetMapDistricts(repository.Object).ExecuteAsync(null);

        result.Should().Be("districts-geojson");
        repository.Verify(item => item.GetDistrictsAsync(0.0005), Times.Once);
    }
}
