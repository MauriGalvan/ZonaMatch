using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Entities;
using NetTopologySuite.Geometries;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class GetNearbySchoolsTests
{
    [Fact]
    public async Task GetNearbySchools_ForwardsRadiusAndFiltersToRepository()
    {
        var repository = new Mock<ISchoolRepository>();
        repository.Setup(item => item.GetNearbyByFiltersAsync(-34.6, -58.4, 1_500, "primary", "public"))
            .ReturnsAsync(Array.Empty<(ZonaMatch.Domain.Entities.School Entity, double DistanceMeters)>());

        var result = await new GetNearbySchools(repository.Object)
            .ExecuteAsync(-34.6, -58.4, 1_500, "primary", "public");

        result.Should().BeEmpty();
        repository.Verify(item => item.GetNearbyByFiltersAsync(-34.6, -58.4, 1_500, "primary", "public"), Times.Once);
    }

    [Fact]
    public async Task GetNearbySchools_MapsTheSchoolAndDistanceToResponse()
    {
        var school = new School
        {
            NaturalKey = "school-1",
            Name = "Primary School",
            Level = "Primary",
            Sector = "Public",
            Location = new Point(-58.4, -34.6) { SRID = 4326 }
        };
        var repository = new Mock<ISchoolRepository>();
        repository.Setup(item => item.GetNearbyByFiltersAsync(-34.6, -58.4, 1_000, null, null))
            .ReturnsAsync(new[] { (Entity: school, DistanceMeters: 125d) });

        var result = await new GetNearbySchools(repository.Object)
            .ExecuteAsync(-34.6, -58.4, 1_000, null, null);

        var response = result.Should().ContainSingle().Which;
        response.NaturalKey.Should().Be("school-1");
        response.Latitude.Should().Be(-34.6);
        response.Longitude.Should().Be(-58.4);
        response.DistanceMeters.Should().Be(125);
    }

    [Fact]
    public async Task GetNearbySchools_WhenCoordinatesAreInvalid_ThrowsBadRequestWithoutQueryingRepository()
    {
        var repository = new Mock<ISchoolRepository>();

        Func<Task> action = () => new GetNearbySchools(repository.Object).ExecuteAsync(100, -58.4, 1_500, null, null);

        await action.Should().ThrowAsync<ZonaMatch.Application.Exceptions.BadRequestException>();
        repository.Verify(item => item.GetNearbyByFiltersAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }
}
