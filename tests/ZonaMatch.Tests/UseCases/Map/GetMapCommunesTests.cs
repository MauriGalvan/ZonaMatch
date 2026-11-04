using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Map;

namespace ZonaMatch.Tests.UseCases.Map;

public class GetMapCommunesTests
{
    [Fact]
    public async Task GetMapCommunes_WhenToleranceExceedsMaximum_ClampsItBeforeQueryingRepository()
    {
        var repository = new Mock<IMapRepository>();
        repository.Setup(item => item.GetCommunesAsync(0.05)).ReturnsAsync("communes-geojson");

        var result = await new GetMapCommunes(repository.Object).ExecuteAsync(0.2);

        result.Should().Be("communes-geojson");
        repository.Verify(item => item.GetCommunesAsync(0.05), Times.Once);
    }
}
