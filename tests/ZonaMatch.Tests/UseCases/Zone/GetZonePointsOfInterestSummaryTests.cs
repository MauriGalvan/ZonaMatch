using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.PointsOfInterest;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetZonePointsOfInterestSummaryTests
{
    [Fact]
    public async Task GetZonePointsOfInterestSummary_ReturnsTypeCountsForTheRequestedZone()
    {
        var repository = new Mock<IPointOfInterestRepository>();
        repository.Setup(item => item.CountByTypeInZoneAsync("villa-luro", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PointOfInterestCount(PointOfInterestCategory.Education, "school", 4) });

        var result = await new GetZonePointsOfInterestSummary(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync("villa-luro");

        result.RadiusMeters.Should().BeNull();
        var category = result.Categories.Single(item => item.Category == PointOfInterestCategory.Education);
        category.Total.Should().Be(4);
        category.Types.Should().ContainSingle().Which.Count.Should().Be(4);
    }

    [Fact]
    public async Task GetZonePointsOfInterestSummary_WhenSlugIsInvalid_ThrowsBadRequestWithoutQueryingRepository()
    {
        var repository = new Mock<IPointOfInterestRepository>();

        Func<Task> action = () => new GetZonePointsOfInterestSummary(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync("Villa Luro");

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.CountByTypeInZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
