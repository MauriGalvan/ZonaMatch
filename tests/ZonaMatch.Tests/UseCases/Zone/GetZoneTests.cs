using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetZoneTests
{
    [Fact]
    public async Task GetZone_WhenZoneExists_ReturnsItsGeoJsonAndNormalizedTolerance()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.GetBySlugAsync("villa-luro", 0.00015, It.IsAny<CancellationToken>()))
            .ReturnsAsync("zone-geojson");

        var result = await new GetZone(repository.Object).ExecuteAsync("villa-luro", null);

        result.Should().Be("zone-geojson");
        repository.Verify(item => item.GetBySlugAsync("villa-luro", 0.00015, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetZone_WhenZoneDoesNotExist_ThrowsNotFoundException()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.GetBySlugAsync("villa-luro", It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        Func<Task> action = () => new GetZone(repository.Object).ExecuteAsync("villa-luro", null);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetZone_WhenSlugIsInvalid_ThrowsBadRequestWithoutQueryingRepository()
    {
        var repository = new Mock<IZoneRepository>();

        Func<Task> action = () => new GetZone(repository.Object).ExecuteAsync("Villa Luro", null);

        await action.Should().ThrowAsync<ZonaMatch.Application.Exceptions.BadRequestException>();
        repository.Verify(item => item.GetBySlugAsync(
            It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
