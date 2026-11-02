using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.DTOs.Zones.Responses;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Scoring
{
    public class CalculateScoresTests
    {
        private readonly FakeZoneProfilesRepository _zoneProfiles = new();
        private readonly FakeRoutingClient _routing = new();
        private readonly FakeZoneRepository _zones = new();
        private readonly CalculateScores _useCase;

        public CalculateScoresTests()
        {
            _useCase = new CalculateScores(_zoneProfiles, _routing, _zones);
        }

        private void AddZone(string slug, int crimes) => _zoneProfiles.Zones.Add(new ZoneProfile
        {
            Slug = slug,
            Name = slug,
            Level = 9,
            Population = 1000,
            AreaKm2 = 1,
            CrimeCount = crimes,
            Latitude = -34.6,
            Longitude = -58.5,
            AverageRent = 600_000,
        });

        private static CalculateScoresRequest BuildRequest() => new()
        {
            Criteria = new() { ["seguridad"] = "alta", ["inventado"] = "alta", ["salud"] = "urgente" },
        };

        [Fact]
        public async Task ExecuteAsync_WhenAddedZoneDoesNotExist_ThrowsNotFoundException()
        {
            AddZone("villa-luro", 30);

            var request = BuildRequest();
            request.AddedZones = ["no-existe"];

            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(request));
        }

        [Fact]
        public async Task ExecuteAsync_WhenUnknownCriteriaAreProvided_IgnoresThem()
        {
            AddZone("villa-luro", 30);

            var zone = Assert.Single((await _useCase.ExecuteAsync(BuildRequest())).Zones);

            Assert.Equal(["seguridad"], zone.Components.Select(component => component.Criterion));
        }

        [Fact]
        public async Task ExecuteAsync_WhenInitialZoneIsOutsideTop_IncludesItAndReturnsRecommendations()
        {
            AddZone("villa-luro", 50);
            for (var i = 1; i <= 7; i++)
                AddZone($"vecina-{i}", i * 10);
            var request = BuildRequest();
            request.InitialZone = "vecina-7";

            var result = await _useCase.ExecuteAsync(request);

            Assert.Equal(CalculateScores.AutomaticTopZones, result.Zones.Count);
            Assert.Contains(result.Zones, zone => zone.Slug == "vecina-7");
            Assert.Equal(3, result.Recommendations.Count);
            Assert.DoesNotContain(result.Recommendations, zone => result.Zones.Any(top => top.Slug == zone.Slug));
        }

        [Fact]
        public async Task ExecuteAsync_WhenRoutingFails_EstimatesTravelTime()
        {
            AddZone("villa-luro", 30);
            _routing.IsAvailable = false;
            var request = BuildRequest();
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4, MaximumTravelTime = "30 min" }];

            var place = Assert.Single(Assert.Single((await _useCase.ExecuteAsync(request)).Zones).Places);

            Assert.True(place.IsEstimated);
            Assert.Equal(30, place.MaximumMinutes);
            Assert.NotNull(place.Minutes);
            Assert.Null(place.DistanceKm);
        }

        [Fact]
        public async Task ExecuteAsync_WhenRentLimitIsFormatted_ParsesTheEnteredAmount()
        {
            AddZone("villa-luro", 30);
            var request = BuildRequest();
            request.MaximumRent = "$ 500.000";

            var zone = Assert.Single((await _useCase.ExecuteAsync(request)).Zones);

            var limit = Assert.Single(zone.Limits);
            Assert.False(limit.IsMet);
            Assert.Contains("500.000", limit.Details);
        }

        [Fact]
        public async Task ExecuteAsync_UsesHouseholdMobilityToMeasureTrips()
        {
            AddZone("villa-luro", 30);
            var request = BuildRequest();
            request.Mobility = ["vehiculo", "a-pie"];
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4 }];

            var result = await _useCase.ExecuteAsync(request);

            Assert.Equal("auto", result.TravelMode);
            Assert.Equal([TravelMode.Car], _routing.Modes.Distinct());
        }

        [Fact]
        public async Task ExecuteAsync_WhenZoneHasStartingPoint_MeasuresItsTripsFromThere()
        {
            AddZone("villa-luro", 30);
            AddZone("floresta", 30);
            var request = BuildRequest();
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.60, Longitude = -58.38 }];
            request.ZoneOrigins = [new ZoneOriginRequest { Slug = "villa-luro", Latitude = -34.64, Longitude = -58.50 }];

            await _useCase.ExecuteAsync(request);

            Assert.Contains((-34.64, -58.50), _routing.Origins);
            Assert.Contains((-34.6, -58.5), _routing.Origins);
        }

        [Fact]
        public async Task ExecuteAsync_WhenStartingPointIsOutsideItsZone_ThrowsBadRequestException()
        {
            AddZone("villa-luro", 30);
            _zones.ContainsStartingPoint = false;
            var request = BuildRequest();
            request.ZoneOrigins = [new ZoneOriginRequest { Slug = "villa-luro", Latitude = -34.5, Longitude = -58.4 }];

            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(request));
        }

        private sealed class FakeZoneRepository : IZoneRepository
        {
            public bool ContainsStartingPoint { get; set; } = true;

            public Task<string?> GetBySlugAsync(string slug, double tolerance, CancellationToken cancellationToken = default) =>
                Task.FromResult<string?>(null);

            public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult(true);

            public Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default) =>
                Task.FromResult(ContainsStartingPoint);

            public Task<IReadOnlyList<ZoneSearchResultResponse>> SearchAsync(string searchFragment, int resultLimit, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ZoneSearchResultResponse>>([]);
        }
      
        [Theory]
        [InlineData("vehiculo", TravelMode.Car)]
        [InlineData("bici", TravelMode.Bicycle)]
        [InlineData("a-pie", TravelMode.Walking)]
        [InlineData("transporte-publico", TravelMode.PublicTransit)]
        public async Task ExecuteAsync_WhenMobilityIsSelected_ReturnsRouteDistance(string mobility, TravelMode expectedMode)
        {
            AddZone("recoleta", 30);
            var request = BuildRequest();
            request.Mobility = [mobility];
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4 }];

            var result = await _useCase.ExecuteAsync(request);
            var place = Assert.Single(Assert.Single(result.Zones).Places);

            Assert.Equal(25, place.Minutes);
            Assert.Equal(8.75, place.DistanceKm!.Value, 3);
            Assert.Equal([expectedMode], _routing.Modes.Distinct());
        }

        [Fact]
        public async Task ExecuteAsync_WhenPlaceHasNoCoordinates_DoesNotCreateARoute()
        {
            AddZone("recoleta", 30);
            var request = BuildRequest();
            request.Places = [new PlaceScoreRequest { Name = "Trabajo" }];

            var result = await _useCase.ExecuteAsync(request);

            Assert.Empty(Assert.Single(result.Zones).Places);
            Assert.Empty(_routing.Modes);
        }

        private sealed class FakeZoneProfilesRepository : IZoneProfilesRepository
        {
            public List<ZoneProfile> Zones { get; } = [];

            public Task<IReadOnlyList<ZoneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ZoneProfile>>(Zones);

            public Task<IReadOnlyList<string>> ListNearbyZoneSlugsAsync(
                IReadOnlyCollection<(double Latitude, double Longitude)> locations,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(Zones.Select(zone => zone.Slug).ToArray());
        }

        private sealed class FakeRoutingClient : IRoutingClient
        {
            public bool IsAvailable { get; set; } = true;
            public List<TravelMode> Modes { get; } = [];
            public List<(double, double)> Origins { get; } = [];

            public Task<TravelTime?> GetTravelTimeAsync(
                double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude, TravelMode mode,
                CancellationToken cancellationToken = default)
            {
                lock (Modes)
                {
                    Modes.Add(mode);
                    Origins.Add((originLatitude, originLongitude));
                }

                return Task.FromResult<TravelTime?>(IsAvailable ? new TravelTime(25, false, 8.75) : null);
            }
        }
    }
}
