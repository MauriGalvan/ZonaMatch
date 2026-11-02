using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.PointsOfInterest;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class GetPointsOfInterestSummaryTests
{
    [Fact]
    public async Task GetPointsOfInterestSummary_AggregatesCountsByCategoryAndType()
    {
        var repository = new Mock<IPointOfInterestRepository>();
        repository.Setup(item => item.CountByTypeAsync(-34.6, -58.4, 2_000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PointOfInterestCount(PointOfInterestCategory.Education, "school", 4),
                new PointOfInterestCount(PointOfInterestCategory.Education, "kindergarten", 2)
            });

        var result = await new GetPointsOfInterestSummary(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync(-34.6, -58.4, 2_000);

        result.RadiusMeters.Should().Be(2_000);
        var education = result.Categories.Single(category => category.Category == PointOfInterestCategory.Education);
        education.Total.Should().Be(6);
        education.Types.Select(type => type.Count).Should().Equal(4, 2);
    }

    [Fact]
    public async Task GetPointsOfInterestSummary_WhenRadiusExceedsMaximum_ThrowsBadRequest()
    {
        var repository = new Mock<IPointOfInterestRepository>();

        Func<Task> action = () => new GetPointsOfInterestSummary(repository.Object, new PointOfInterestResponseBuilder())
            .ExecuteAsync(-34.6, -58.4, 5_001);

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.CountByTypeAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
