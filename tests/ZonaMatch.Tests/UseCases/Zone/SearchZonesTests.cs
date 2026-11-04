using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Application.DTOs.Zone.Responses;

namespace ZonaMatch.Tests.UseCases.Zone;

public class SearchZonesTests
{
    [Fact]
    public async Task SearchZones_WhenSearchTextIsTooShort_ReturnsEmptyWithoutQueryingRepository()
    {
        var repository = new Mock<IZoneRepository>();

        var result = await new SearchZones(repository.Object).ExecuteAsync("A", 10);

        result.Should().BeEmpty();
        repository.Verify(item => item.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchZones_WhenSearchTextIsValid_SendsNormalizedSlugAndLimitToRepository()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.SearchAsync("villa-luro", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneSearchResultResponse[] { new("villa-luro", "Villa Luro", 9, [], 1.5) });

        var result = await new SearchZones(repository.Object).ExecuteAsync("Villa Luro", 5);

        result.Should().ContainSingle().Which.Slug.Should().Be("villa-luro");
    }

    [Fact]
    public async Task SearchZones_WhenLimitIsOutOfRange_ThrowsBadRequestException()
    {
        var repository = new Mock<IZoneRepository>();

        Func<Task> action = () => new SearchZones(repository.Object).ExecuteAsync("Villa Luro", 21);

        await action.Should().ThrowAsync<BadRequestException>();
    }
}
