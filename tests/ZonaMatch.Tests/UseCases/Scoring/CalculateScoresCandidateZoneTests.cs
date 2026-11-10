using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Scoring;

public class CalculateScoresCandidateZoneTests
{
    private const double PlaceLatitude = -34.6;
    private const double PlaceLongitude = -58.4;

    [Fact]
    public async Task CalculateScores_WhenNoZoneDataExists_ThrowsNotFoundException()
    {
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ZoneProfile>());
        var useCase = new CalculateScores(
            zoneProfiles.Object, new Mock<IRoutingClient>().Object, new Mock<IZoneRepository>().Object);

        Func<Task> action = () => useCase.CalculateAsync(new CalculateScoresRequest());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CalculateScores_WhenPlacesHaveLocations_RanksOnlyRepositoryCandidateZones()
    {
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Zone("point-zone", 10),
                Zone("neighbor-zone", 20),
                Zone("far-zone", 100)
            });
        zoneProfiles.Setup(repository => repository.ListNearbyZoneSlugsAsync(
                It.IsAny<IReadOnlyCollection<(double Latitude, double Longitude)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "point-zone", "neighbor-zone" });
        var routing = new Mock<IRoutingClient>();
        routing.Setup(client => client.GetTravelTimeAsync(
                It.IsAny<double>(), It.IsAny<double>(), PlaceLatitude, PlaceLongitude,
                It.IsAny<TravelMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelTime(20, false, 2));
        var request = new CalculateScoresRequest
        {
            Criteria = new() { [ScoringEngine.Safety] = "alta" },
            Places = [new PlaceScoreRequest { Name = "Work", Latitude = PlaceLatitude, Longitude = PlaceLongitude }]
        };

        var result = await new CalculateScores(zoneProfiles.Object, routing.Object, new Mock<IZoneRepository>().Object)
            .CalculateAsync(request);

        var rankedSlugs = result.Zones.Select(zone => zone.Slug).ToArray();
        rankedSlugs.Should().HaveCount(2);
        rankedSlugs.Should().Contain("point-zone");
        rankedSlugs.Should().Contain("neighbor-zone");
        rankedSlugs.Should().NotContain("far-zone");
        zoneProfiles.Verify(repository => repository.ListNearbyZoneSlugsAsync(
            It.Is<IReadOnlyCollection<(double Latitude, double Longitude)>>(locations =>
                locations.Count == 1
                && locations.Single().Latitude == PlaceLatitude
                && locations.Single().Longitude == PlaceLongitude),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateScores_WhenNoCandidateZoneContainsThePlaces_ThrowsBadRequestBeforeRouting()
    {
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Zone("far-zone", 10) });
        zoneProfiles.Setup(repository => repository.ListNearbyZoneSlugsAsync(
                It.IsAny<IReadOnlyCollection<(double Latitude, double Longitude)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var routing = new Mock<IRoutingClient>();
        var request = new CalculateScoresRequest
        {
            Criteria = new() { [ScoringEngine.Safety] = "alta" },
            Places = [new PlaceScoreRequest { Name = "Work", Latitude = PlaceLatitude, Longitude = PlaceLongitude }]
        };

        Func<Task> action = () => new CalculateScores(zoneProfiles.Object, routing.Object, new Mock<IZoneRepository>().Object)
            .CalculateAsync(request);

        await action.Should().ThrowAsync<BadRequestException>();
        routing.Verify(client => client.GetTravelTimeAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<TravelMode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ZoneProfile Zone(string slug, int crimeCount) => new()
    {
        Slug = slug,
        Name = slug,
        Level = 9,
        Population = 1_000,
        CrimeCount = crimeCount,
        Latitude = -34.6,
        Longitude = -58.5
    };
}
