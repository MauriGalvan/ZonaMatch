using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.UseCases.Map;

namespace ZonaMatch.Tests.UseCases.Map;

public class GetMapCensusTractsTests
{
    private static readonly BoundingBox ValidViewport = new(-59, -35, -58, -34);

    [Fact]
    public async Task GetMapCensusTracts_WhenViewportIsInvalid_ThrowsBadRequestBeforeQueryingRepository()
    {
        var repository = new Mock<IMapRepository>();
        var invalidViewport = new BoundingBox(-58, -35, -59, -34);

        Func<Task> action = () => new GetMapCensusTracts(repository.Object).ExecuteAsync(invalidViewport, null);

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.CountCensusTractsAsync(It.IsAny<BoundingBox>()), Times.Never);
    }

    [Fact]
    public async Task GetMapCensusTracts_WhenViewportHasTooManyFeatures_ThrowsBadRequest()
    {
        var repository = new Mock<IMapRepository>();
        repository.Setup(item => item.CountCensusTractsAsync(ValidViewport))
            .ReturnsAsync(MapQueryPolicy.MaximumFeaturesPerQuery + 1);

        Func<Task> action = () => new GetMapCensusTracts(repository.Object).ExecuteAsync(ValidViewport, null);

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetCensusTractsAsync(
            It.IsAny<BoundingBox>(), It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task GetMapCensusTracts_WhenViewportIsValid_UsesViewportBasedTolerance()
    {
        var repository = new Mock<IMapRepository>();
        repository.Setup(item => item.CountCensusTractsAsync(ValidViewport)).ReturnsAsync(100);
        repository.Setup(item => item.GetCensusTractsAsync(ValidViewport, 1d / 1500))
            .ReturnsAsync("census-tracts-geojson");

        var result = await new GetMapCensusTracts(repository.Object).ExecuteAsync(ValidViewport, null);

        result.Should().Be("census-tracts-geojson");
        repository.Verify(item => item.GetCensusTractsAsync(ValidViewport, 1d / 1500), Times.Once);
    }
}
