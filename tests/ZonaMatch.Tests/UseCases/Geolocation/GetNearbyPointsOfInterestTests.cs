using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.PointOfInterest.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class GetNearbyPointsOfInterestTests
{
    [Fact]
    public async Task GetNearbyPointsOfInterest_WhenCategoriesAreSelected_ForwardsCategoriesAndRadius()
    {
        var repository = new Mock<IPointOfInterestRepository>();
        var expectedCategories = new[] { PointOfInterestCategory.Education };
        repository.Setup(item => item.GetNearbyAsync(
                -34.6,
                -58.4,
                1_000,
                It.Is<IReadOnlyCollection<string>>(categories => categories.SequenceEqual(expectedCategories)),
                PointOfInterestResponseBuilder.MaximumPoints + 1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await new GetNearbyPointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync(-34.6, -58.4, 1_000, "educacion");

        result.Points.Should().BeEmpty();
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task GetNearbyPointsOfInterest_WhenRadiusExceedsMaximum_ThrowsBadRequest()
    {
        var repository = new Mock<IPointOfInterestRepository>();

        Func<Task> action = () => new GetNearbyPointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync(-34.6, -58.4, 5_001, null);

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetNearbyAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(91, -58.4, 1_000)]
    [InlineData(-34.6, -58.4, -1)]
    public async Task GetNearbyPointsOfInterest_WhenCoordinatesOrRadiusAreInvalid_ThrowsBadRequest(
        double latitude,
        double longitude,
        double radiusMeters)
    {
        var repository = new Mock<IPointOfInterestRepository>();

        Func<Task> action = () => new GetNearbyPointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync(latitude, longitude, radiusMeters, null);

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetNearbyAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetNearbyPointsOfInterest_WhenCategoriesContainUnknownValues_ThrowsBadRequestWithoutQueryingRepository()
    {
        var repository = new Mock<IPointOfInterestRepository>();

        Func<Task> action = () => new GetNearbyPointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync(-34.6, -58.4, 1_000, "unknown-category");

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetNearbyAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetNearbyPointsOfInterest_WhenRepositoryReturnsMoreThanMaximum_CapsResultsAndMarksThemAsTruncated()
    {
        var repository = new Mock<IPointOfInterestRepository>();
        var points = Enumerable.Range(0, PointOfInterestResponseBuilder.MaximumPoints + 1)
            .Select(index => new PointOfInterestResponse(
                $"poi-{index}", null, PointOfInterestCategory.Education, "school", null, -34.6, -58.4, index, "osm"))
            .ToArray();
        repository.Setup(item => item.GetNearbyAsync(
                -34.6,
                -58.4,
                1_000,
                It.IsAny<IReadOnlyCollection<string>>(),
                PointOfInterestResponseBuilder.MaximumPoints + 1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);

        var result = await new GetNearbyPointsOfInterest(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync(-34.6, -58.4, 1_000, null);

        result.Points.Should().HaveCount(PointOfInterestResponseBuilder.MaximumPoints);
        result.Points.First().Id.Should().Be("poi-0");
        result.Truncated.Should().BeTrue();
    }
}
