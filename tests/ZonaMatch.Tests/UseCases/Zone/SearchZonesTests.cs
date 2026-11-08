using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Entities;
using DomainZone = ZonaMatch.Domain.Entities.Zone;

namespace ZonaMatch.Tests.UseCases.Zone;

public class SearchZonesTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("a .")]
    [InlineData("Ñ-")]
    public async Task SearchZones_WhenSearchTextHasFewerThanTwoLettersOrDigits_ReturnsEmptyWithoutQueryingRepository(string search)
    {
        var repository = new Mock<IZoneRepository>();

        var result = await new SearchZones(repository.Object).ExecuteAsync(search, 10);

        result.Should().BeEmpty();
        repository.Verify(item => item.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchZones_WhenSearchTextIsValid_SendsTheTextAsTypedAndMapsTheZones()
    {
        var repository = new Mock<IZoneRepository>();
        repository.Setup(item => item.SearchAsync("Villa Luro", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DomainZone
                {
                    OsmId = -1, Slug = "villa-luro", Name = "Villa Luro", AdministrativeLevel = 10, AreaKm2 = 2.6,
                    Jurisdictions = [new ZoneJurisdiction("Comuna 10", 5)]
                }
            ]);

        var result = await new SearchZones(repository.Object).ExecuteAsync("Villa Luro", 5);

        var zone = result.Should().ContainSingle().Subject;
        zone.Slug.Should().Be("villa-luro");
        zone.Name.Should().Be("Villa Luro");
        zone.AdministrativeLevel.Should().Be(10);
        zone.AreaKm2.Should().Be(2.6);
        zone.Jurisdictions.Should().ContainSingle().Which.Name.Should().Be("Comuna 10");
    }

    [Fact]
    public async Task SearchZones_WhenSearchTextIsBlank_ThrowsBadRequestException()
    {
        var repository = new Mock<IZoneRepository>();

        Func<Task> action = () => new SearchZones(repository.Object).ExecuteAsync("  ", 10);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task SearchZones_WhenLimitIsOutOfRange_ThrowsBadRequestException()
    {
        var repository = new Mock<IZoneRepository>();

        Func<Task> action = () => new SearchZones(repository.Object).ExecuteAsync("Villa Luro", 21);

        await action.Should().ThrowAsync<BadRequestException>();
    }
}
