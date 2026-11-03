using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetZonePointsOfInterestTests
{
    [Fact]
    public async Task GetZonePointsOfInterest_ForValidCategories_QueriesTheZoneWithTheExpectedLimit()
    {
        var repository = new Mock<IPointOfInterestRepository>();
        var expectedCategories = new[] { PointOfInterestCategory.Education };
        repository.Setup(item => item.GetInZoneAsync(
                "villa-luro",
                It.Is<IReadOnlyCollection<string>>(categories => categories.SequenceEqual(expectedCategories)),
                PointOfInterestResponseBuilder.MaximumPoints + 1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await new GetZonePointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync("villa-luro", "educacion");

        result.Points.Should().BeEmpty();
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task GetZonePointsOfInterest_WhenCategoryIsUnknown_ThrowsBadRequestWithoutQueryingRepository()
    {
        var repository = new Mock<IPointOfInterestRepository>();

        Func<Task> action = () => new GetZonePointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync("villa-luro", "unknown");

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetInZoneAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetZonePointsOfInterest_WhenSlugIsInvalid_ThrowsBadRequestWithoutQueryingRepository()
    {
        var repository = new Mock<IPointOfInterestRepository>();

        Func<Task> action = () => new GetZonePointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync("Villa Luro", "educacion");

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetInZoneAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
