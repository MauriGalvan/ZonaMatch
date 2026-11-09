using System.Text.Json.Nodes;
using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Entities;
using DomainZone = ZonaMatch.Domain.Entities.Zone;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetZoneTests
{
    private static readonly DomainZone Nunez = new()
    {
        OsmId = -1224652,
        Slug = "nunez",
        Name = "Núñez",
        AdministrativeLevel = 10,
        AreaKm2 = 3.89,
        Jurisdictions = [new ZoneJurisdiction("Comuna 13", 5), new ZoneJurisdiction("Ciudad Autónoma de Buenos Aires", 4)],
        Boundary = new ZoneBoundary(-34.54, -58.46, """{"type":"Polygon","coordinates":[[[-58.4,-34.5],[-58.5,-34.5],[-58.5,-34.6],[-58.4,-34.5]]]}""")
    };

    [Fact]
    public async Task GetZone_WhenZoneExists_ReturnsGeoJsonFeatureWithItsProperties()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.GetBySlugAsync("nunez", 0.00015, It.IsAny<CancellationToken>())).ReturnsAsync(Nunez);

        var result = await new GetZone(repository.Object).ExecuteAsync("nunez", null);

        var feature = JsonNode.Parse(result)!;
        feature["type"]!.GetValue<string>().Should().Be("Feature");
        feature["id"]!.GetValue<string>().Should().Be("r1224652");
        var properties = feature["properties"]!;
        properties["slug"]!.GetValue<string>().Should().Be("nunez");
        properties["name"]!.GetValue<string>().Should().Be("Núñez");
        properties["administrativeLevel"]!.GetValue<int>().Should().Be(10);
        properties["areaKm2"]!.GetValue<double>().Should().Be(3.89);
        properties["jurisdictions"]!.AsArray().Select(item => item!["name"]!.GetValue<string>())
            .Should().Equal("Comuna 13", "Ciudad Autónoma de Buenos Aires");
        properties["jurisdictions"]![0]!["administrativeLevel"]!.GetValue<int>().Should().Be(5);
        properties["center"]!["latitude"]!.GetValue<double>().Should().Be(-34.54);
        properties["center"]!["longitude"]!.GetValue<double>().Should().Be(-58.46);
        feature["geometry"]!["type"]!.GetValue<string>().Should().Be("Polygon");
    }

    [Fact]
    public async Task GetZone_WhenNameHasAccents_KeepsThemWithoutEscaping()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.GetBySlugAsync("nunez", It.IsAny<double>(), It.IsAny<CancellationToken>())).ReturnsAsync(Nunez);

        var result = await new GetZone(repository.Object).ExecuteAsync("nunez", null);

        result.Should().Contain("\"name\":\"Núñez\"");
    }

    [Fact]
    public async Task GetZone_WhenToleranceIsNegative_AsksForTheBoundaryWithoutSimplifying()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.GetBySlugAsync("nunez", 0, It.IsAny<CancellationToken>())).ReturnsAsync(Nunez);

        await new GetZone(repository.Object).ExecuteAsync("nunez", -1);

        repository.Verify(item => item.GetBySlugAsync("nunez", 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetZone_WhenZoneComesWithoutBoundary_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.GetBySlugAsync("nunez", It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DomainZone { Slug = "nunez" });

        Func<Task> action = () => new GetZone(repository.Object).ExecuteAsync("nunez", null);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetZone_WhenZoneDoesNotExist_ThrowsNotFoundException()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.GetBySlugAsync("villa-luro", It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainZone?)null);

        Func<Task> action = () => new GetZone(repository.Object).ExecuteAsync("villa-luro", null);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetZone_WhenSlugIsInvalid_ThrowsBadRequestWithoutQueryingRepository()
    {
        var repository = new Mock<IZoneRepository>();

        Func<Task> action = () => new GetZone(repository.Object).ExecuteAsync("Villa Luro", null);

        await action.Should().ThrowAsync<BadRequestException>();
        repository.Verify(item => item.GetBySlugAsync(
            It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
