using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class GetLocationSummaryTests
{
    [Fact]
    public async Task GetLocationSummary_ReturnsBoundariesAndNearbySchoolsForCoordinates()
    {
        var districts = new Mock<IDistrictRepository>();
        districts.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync(new District { Key = "district-1", Name = "District One", Province = "Buenos Aires" });
        var communes = new Mock<ICommuneRepository>();
        communes.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync((Commune?)null);
        var schools = new Mock<ISchoolRepository>();
        schools.Setup(repository => repository.GetNearbyByFiltersAsync(-34.6, -58.4, 1_000, null, null))
            .ReturnsAsync(Array.Empty<(School Entity, double DistanceMeters)>());

        var result = await new GetLocationSummary(
                new LocationBoundaryLookup(districts.Object, communes.Object), schools.Object)
            .ExecuteAsync(-34.6, -58.4, 1_000);

        result.Location.Latitude.Should().Be(-34.6);
        result.Location.Longitude.Should().Be(-58.4);
        result.District!.Name.Should().Be("District One");
        result.Commune.Should().BeNull();
        result.NearbySchools.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocationSummary_WhenCommuneAndSchoolsExist_MapsTheirDetails()
    {
        var districts = new Mock<IDistrictRepository>();
        districts.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync((District?)null);
        var communes = new Mock<ICommuneRepository>();
        communes.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync(new Commune { Key = "commune-1", Name = "Comuna Uno" });
        var school = new School
        {
            NaturalKey = "school-1",
            Name = "Escuela Uno",
            Level = "Primario",
            Sector = "Público",
            Address = "Calle 123",
            Locality = "Villa Luro",
            Location = new Point(-58.4, -34.6) { SRID = 4326 }
        };
        var schools = new Mock<ISchoolRepository>();
        schools.Setup(repository => repository.GetNearbyByFiltersAsync(-34.6, -58.4, 1_000, null, null))
            .ReturnsAsync(new[] { (Entity: school, DistanceMeters: 250d) });

        var result = await new GetLocationSummary(
                new LocationBoundaryLookup(districts.Object, communes.Object), schools.Object)
            .ExecuteAsync(-34.6, -58.4, 1_000);

        result.District.Should().BeNull();
        result.Commune.Should().NotBeNull();
        result.Commune!.Key.Should().Be("commune-1");
        result.Commune!.Name.Should().Be("Comuna Uno");
        var schoolResponse = result.NearbySchools.Should().ContainSingle().Which;
        schoolResponse.NaturalKey.Should().Be("school-1");
        schoolResponse.Name.Should().Be("Escuela Uno");
        schoolResponse.Latitude.Should().Be(-34.6);
        schoolResponse.Longitude.Should().Be(-58.4);
        schoolResponse.DistanceMeters.Should().Be(250);
    }

    [Fact]
    public async Task GetLocationSummary_WhenCoordinatesAreInvalid_ThrowsBadRequestWithoutQueryingRepositories()
    {
        var districts = new Mock<IDistrictRepository>();
        var communes = new Mock<ICommuneRepository>();
        var schools = new Mock<ISchoolRepository>();
        var useCase = new GetLocationSummary(
            new LocationBoundaryLookup(districts.Object, communes.Object), schools.Object);

        Func<Task> action = () => useCase.ExecuteAsync(91, -58.4, 1_000);

        await action.Should().ThrowAsync<BadRequestException>();
        districts.Verify(repository => repository.GetContainingPointAsync(
            It.IsAny<double>(), It.IsAny<double>()), Times.Never);
        communes.Verify(repository => repository.GetContainingPointAsync(
            It.IsAny<double>(), It.IsAny<double>()), Times.Never);
        schools.Verify(repository => repository.GetNearbyByFiltersAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }
}
