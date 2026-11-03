using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class GetNearestSchoolsTests
{
    [Fact]
    public async Task GetNearestSchools_WhenQuantityIsPositive_RequestsThatNumberOfSchools()
    {
        var repository = new Mock<ISchoolRepository>();
        repository.Setup(item => item.GetNearestAsync(-34.6, -58.4, 4))
            .ReturnsAsync(Array.Empty<(School Entity, double DistanceMeters)>());

        var result = await new GetNearestSchools(repository.Object).ExecuteAsync(-34.6, -58.4, 4);

        result.Should().BeEmpty();
        repository.Verify(item => item.GetNearestAsync(-34.6, -58.4, 4), Times.Once);
    }

    [Fact]
    public async Task GetNearestSchools_WhenQuantityIsNotPositive_ThrowsBadRequest()
    {
        var repository = new Mock<ISchoolRepository>();

        Func<Task> action = () => new GetNearestSchools(repository.Object).ExecuteAsync(-34.6, -58.4, 0);

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetNearestAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>()), Times.Never);
    }
}
