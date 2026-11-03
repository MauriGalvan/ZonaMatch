using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class GetDistrictByLocationTests
{
    [Fact]
    public async Task GetDistrictByLocation_WhenPointIsInsideDistrict_ReturnsItsDetails()
    {
        var districts = new Mock<IDistrictRepository>();
        districts.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync(new District { Key = "district-1", Name = "District One", Province = "Buenos Aires" });
        var lookup = new LocationBoundaryLookup(districts.Object, new Mock<ICommuneRepository>().Object);

        var result = await new GetDistrictByLocation(lookup).ExecuteAsync(-34.6, -58.4);

        result.Key.Should().Be("district-1");
        result.Province.Should().Be("Buenos Aires");
    }

    [Fact]
    public async Task GetDistrictByLocation_WhenPointIsOutsideEveryDistrict_ThrowsNotFound()
    {
        var districts = new Mock<IDistrictRepository>();
        districts.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync((District?)null);
        var useCase = new GetDistrictByLocation(new LocationBoundaryLookup(
            districts.Object, new Mock<ICommuneRepository>().Object));

        Func<Task> action = () => useCase.ExecuteAsync(-34.6, -58.4);

        await action.Should().ThrowAsync<NotFoundException>();
    }
}
