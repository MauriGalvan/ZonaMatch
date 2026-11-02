using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class GetCommuneByLocationTests
{
    [Fact]
    public async Task GetCommuneByLocation_WhenPointIsInsideCommune_ReturnsItsDetails()
    {
        var communes = new Mock<ICommuneRepository>();
        communes.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync(new Commune { Key = "comuna-1", Name = "Comuna 1" });
        var lookup = new LocationBoundaryLookup(new Mock<IDistrictRepository>().Object, communes.Object);

        var result = await new GetCommuneByLocation(lookup).ExecuteAsync(-34.6, -58.4);

        result.Key.Should().Be("comuna-1");
        result.Name.Should().Be("Comuna 1");
    }

    [Fact]
    public async Task GetCommuneByLocation_WhenPointIsOutsideEveryCommune_ThrowsNotFound()
    {
        var communes = new Mock<ICommuneRepository>();
        communes.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync((Commune?)null);
        var useCase = new GetCommuneByLocation(new LocationBoundaryLookup(
            new Mock<IDistrictRepository>().Object, communes.Object));

        Func<Task> action = () => useCase.ExecuteAsync(-34.6, -58.4);

        await action.Should().ThrowAsync<NotFoundException>();
    }
}
