using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.UseCases.Map;

namespace ZonaMatch.Tests.UseCases.Map;

public class GetMapSchoolsTests
{
    private static readonly BoundingBox ValidViewport = new(-59, -35, -58, -34);

    [Fact]
    public async Task GetMapSchools_WhenFeatureLimitIsExceeded_ThrowsBadRequestWithoutLoadingGeoJson()
    {
        var repository = new Mock<IMapRepository>();
        repository.Setup(item => item.CountSchoolsAsync(ValidViewport, "primary", "public"))
            .ReturnsAsync(MapQueryPolicy.MaximumFeaturesPerQuery + 1);

        Func<Task> action = () => new GetMapSchools(repository.Object)
            .ExecuteAsync(ValidViewport, "primary", "public");

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetSchoolsAsync(
            It.IsAny<BoundingBox>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GetMapSchools_WhenQueryIsWithinLimit_ReturnsGeoJsonWithRequestedFilters()
    {
        var repository = new Mock<IMapRepository>();
        repository.Setup(item => item.CountSchoolsAsync(ValidViewport, "primary", "public")).ReturnsAsync(25);
        repository.Setup(item => item.GetSchoolsAsync(ValidViewport, "primary", "public"))
            .ReturnsAsync("schools-geojson");

        var result = await new GetMapSchools(repository.Object)
            .ExecuteAsync(ValidViewport, "primary", "public");

        result.Should().Be("schools-geojson");
        repository.Verify(item => item.GetSchoolsAsync(ValidViewport, "primary", "public"), Times.Once);
    }
}
