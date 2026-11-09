using FluentAssertions;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.DTOs.Zone.Responses;
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

        private void AddZone(string slug, int crimes, decimal rent = 600_000, int level = 9) => _zoneProfiles.Zones.Add(new ZoneProfile
        {
            Slug = slug,
            Name = slug,
            Level = level,
            Population = 1000,
            AreaKm2 = 1,
            CrimeCount = crimes,
            Latitude = -34.6,
            Longitude = -58.5,
            AverageRent = rent,
        });

        private static CalculateScoresRequest BuildRequest() => new()
        {
            Criteria = new() { ["seguridad"] = "alta", ["inventado"] = "alta", ["salud"] = "urgente" },
        };

        [Fact]
        public async Task CalculateAsync_WhenAddedZoneDoesNotExist_ThrowsNotFoundException()
        {
            AddZone("villa-luro", 30);

            var request = BuildRequest();
            request.AddedZones = ["no-existe"];

            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.CalculateAsync(request));
        }

        [Fact]
        public async Task CalculateAsync_WhenUnknownCriteriaAreProvided_IgnoresThem()
        {
            AddZone("villa-luro", 30);

            var zone = Assert.Single((await _useCase.CalculateAsync(BuildRequest())).Zones);

            Assert.Equal(["seguridad"], zone.Components.Select(component => component.Criterion));
        }

        [Fact]
        public async Task CalculateAsync_WhenInitialZoneIsOutsideTop_IncludesItAndReturnsRecommendations()
        {
            AddZone("villa-luro", 50);
            for (var i = 1; i <= 7; i++)
                AddZone($"vecina-{i}", i * 10);
            var request = BuildRequest();
            request.InitialZone = "vecina-7";

            var result = await _useCase.CalculateAsync(request);

            Assert.Equal(CalculateScores.AutomaticTopZones, result.Zones.Count);
            Assert.Contains(result.Zones, zone => zone.Slug == "vecina-7");
            Assert.Equal(3, result.Recommendations.Count);
            Assert.DoesNotContain(result.Recommendations, zone => result.Zones.Any(top => top.Slug == zone.Slug));
        }

        private static CalculateScoresRequest RequestWithWorkplace()
        {
            var request = BuildRequest();
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4, MaximumTravelTime = "60 min" }];
            request.MaximumRent = "800000";
            return request;
        }

        [Fact]
        public async Task CalculateAsync_WhenNearbyZonesScoreUnderTheMinimum_AddsBetterZonesFromTheRestOfTheCatalog()
        {
            AddZone("insegura-1", 400, rent: 3_000_000);
            AddZone("insegura-2", 400, rent: 3_000_000);
            AddZone("tranquila", 0);
            _zoneProfiles.NearbySlugs = ["insegura-1", "insegura-2"];

            var result = await _useCase.CalculateAsync(RequestWithWorkplace());

            result.SearchExpanded.Should().BeTrue();
            result.Zones[0].Slug.Should().Be("tranquila");
            result.Zones.Select(zone => zone.Slug).Should().Contain(["insegura-1", "insegura-2"]);
        }

        [Fact]
        public async Task CalculateAsync_WhenANearbyZoneReachesTheMinimum_DoesNotExpandTheSearch()
        {
            AddZone("tranquila", 0);
            AddZone("insegura", 400);
            _zoneProfiles.NearbySlugs = ["tranquila"];

            var result = await _useCase.CalculateAsync(RequestWithWorkplace());

            result.SearchExpanded.Should().BeFalse();
            result.Zones.Should().ContainSingle(zone => zone.Slug == "tranquila");
            result.Zones.Should().NotContain(zone => zone.Slug == "insegura");
        }

        [Fact]
        public async Task CalculateAsync_WhenHouseholdHasABike_MeasuresTheExpandedZonesByBikeToo()
        {
            AddZone("barrio-caro", 400, rent: 3_000_000);
            AddZone("barrio-tranquilo", 0);
            _zoneProfiles.NearbySlugs = ["barrio-caro"];
            var request = RequestWithWorkplace();
            request.Mobility = ["transporte-publico", "bici"];

            var result = await _useCase.CalculateAsync(request);

            result.SearchExpanded.Should().BeTrue();
            // One trip by public transit and one by bike for each of the two zones
            _routing.Modes.Count(mode => mode == TravelMode.Bicycle).Should().Be(2);
        }

        [Fact]
        public async Task CalculateAsync_WhenExpandingTheSearch_OnlyAddsZonesOfTheSameLevel()
        {
            AddZone("barrio-caro", 400, rent: 3_000_000);
            AddZone("comuna-tranquila", 0, level: 5);
            AddZone("barrio-tranquilo", 0);
            _zoneProfiles.NearbySlugs = ["barrio-caro"];

            var result = await _useCase.CalculateAsync(RequestWithWorkplace());

            result.SearchExpanded.Should().BeTrue();
            result.Zones.Select(zone => zone.Slug).Should().Contain("barrio-tranquilo").And.NotContain("comuna-tranquila");
            result.Recommendations.Should().NotContain(zone => zone.Slug == "comuna-tranquila");
        }

        [Fact]
        public async Task CalculateAsync_WhenAFarZoneHasNoRouteToThePlaces_DoesNotAddIt()
        {
            AddZone("insegura", 400, rent: 3_000_000);
            AddZone("sin-viaje", 0);
            _zoneProfiles.NearbySlugs = ["insegura"];
            _routing.UnreachableLatitude = -34.6;

            var result = await _useCase.CalculateAsync(RequestWithWorkplace());

            result.SearchExpanded.Should().BeFalse();
            result.Zones.Should().NotContain(zone => zone.Slug == "sin-viaje");
        }

        [Fact]
        public async Task CalculateAsync_WhenTheRestOfTheCatalogIsNotBetter_DoesNotFlagTheSearchAsExpanded()
        {
            AddZone("insegura-1", 400, rent: 3_000_000);
            AddZone("insegura-2", 500, rent: 3_000_000);
            _zoneProfiles.NearbySlugs = ["insegura-1"];

            var result = await _useCase.CalculateAsync(RequestWithWorkplace());

            result.SearchExpanded.Should().BeFalse();
            result.Zones[0].Slug.Should().Be("insegura-1");
        }

        [Fact]
        public async Task CalculateAsync_WhenRoutingFails_EstimatesTravelTime()
        {
            AddZone("villa-luro", 30);
            _routing.IsAvailable = false;
            var request = BuildRequest();
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4, MaximumTravelTime = "30 min" }];

            var place = Assert.Single(Assert.Single((await _useCase.CalculateAsync(request)).Zones).Places);

            Assert.True(place.IsEstimated);
            Assert.Equal(30, place.MaximumMinutes);
            Assert.NotNull(place.Minutes);
            Assert.Null(place.DistanceKm);
        }

        [Fact]
        public async Task CalculateAsync_WhenRentLimitIsFormatted_ParsesTheEnteredAmount()
        {
            AddZone("villa-luro", 30);
            var request = BuildRequest();
            request.MaximumRent = "$ 500.000";

            var zone = Assert.Single((await _useCase.CalculateAsync(request)).Zones);

            var limit = Assert.Single(zone.Limits);
            Assert.False(limit.IsMet);
            Assert.Contains("500.000", limit.Details);
        }

        [Fact]
        public async Task CalculateAsync_UsesHouseholdMobilityToMeasureTrips()
        {
            AddZone("villa-luro", 30);
            var request = BuildRequest();
            request.Mobility = ["vehiculo", "a-pie"];
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4 }];

            var result = await _useCase.CalculateAsync(request);

            Assert.Equal("auto", result.TravelMode);
            Assert.Equal([TravelMode.Car], _routing.Modes.Distinct());
        }

        [Fact]
        public async Task CalculateAsync_WhenZoneHasStartingPoint_MeasuresItsTripsFromThere()
        {
            AddZone("villa-luro", 30);
            AddZone("floresta", 30);
            var request = BuildRequest();
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.60, Longitude = -58.38 }];
            request.ZoneOrigins = [new ZoneOriginRequest { Slug = "villa-luro", Latitude = -34.64, Longitude = -58.50 }];

            await _useCase.CalculateAsync(request);

            Assert.Contains((-34.64, -58.50), _routing.Origins);
            Assert.Contains((-34.6, -58.5), _routing.Origins);
        }

        [Fact]
        public async Task CalculateAsync_WhenStartingPointIsOutsideItsZone_ThrowsBadRequestException()
        {
            AddZone("villa-luro", 30);
            _zones.ContainsStartingPoint = false;
            var request = BuildRequest();
            request.ZoneOrigins = [new ZoneOriginRequest { Slug = "villa-luro", Latitude = -34.5, Longitude = -58.4 }];

            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.CalculateAsync(request));
        }

        private static CalculateScoresRequest RequestWithPlaces(int count, string? maximumRent = "800000") => new()
        {
            Criteria = new() { ["seguridad"] = "alta" },
            MaximumRent = maximumRent,
            Places = Enumerable.Range(1, count)
                .Select(index => new PlaceScoreRequest { Name = $"Lugar {index}", MaximumTravelTime = "45 min" })
                .ToList(),
        };

        [Fact]
        public async Task ExecuteAsync_WhenGuestSendsMorePlacesThanAllowed_ThrowsBadRequestException()
        {
            AddZone("villa-luro", 30);

            var action = () => _useCase.ExecuteAsync(RequestWithPlaces(ScoringPlaceLimits.Guest + 1), isGuest: true);

            await action.Should().ThrowAsync<BadRequestException>().WithMessage(ScoringPlaceLimits.GuestMessage);
        }

        [Fact]
        public async Task ExecuteAsync_WhenSignedInUserSendsMorePlacesThanAGuest_CalculatesTheRanking()
        {
            AddZone("villa-luro", 30);

            var result = await _useCase.ExecuteAsync(RequestWithPlaces(ScoringPlaceLimits.Guest + 1), isGuest: false);

            result.Zones.Should().ContainSingle();
        }

        [Fact]
        public async Task ExecuteAsync_WhenMonthlyBudgetIsMissing_ThrowsBadRequestException()
        {
            AddZone("villa-luro", 30);

            var action = () => _useCase.ExecuteAsync(RequestWithPlaces(1, maximumRent: null), isGuest: false);

            await action.Should().ThrowAsync<BadRequestException>().WithMessage("El gasto máximo por mes es obligatorio.");
        }

        private sealed class FakeZoneRepository : IZoneRepository
        {
            public bool ContainsStartingPoint { get; set; } = true;

            public Task<ZonaMatch.Domain.Entities.Zone?> GetBySlugAsync(string slug, double tolerance, CancellationToken cancellationToken = default) =>
                Task.FromResult<ZonaMatch.Domain.Entities.Zone?>(null);

            public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult(true);

            public Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default) =>
                Task.FromResult(ContainsStartingPoint);

            public Task<IReadOnlyList<ZonaMatch.Domain.Entities.Zone>> SearchAsync(string searchText, int resultLimit, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ZonaMatch.Domain.Entities.Zone>>([]);
        }
      
        [Theory]
        [InlineData("vehiculo", TravelMode.Car)]
        [InlineData("bici", TravelMode.Bicycle)]
        [InlineData("a-pie", TravelMode.Walking)]
        [InlineData("transporte-publico", TravelMode.PublicTransit)]
        public async Task CalculateAsync_WhenMobilityIsSelected_ReturnsRouteDistance(string mobility, TravelMode expectedMode)
        {
            AddZone("recoleta", 30);
            var request = BuildRequest();
            request.Mobility = [mobility];
            request.Places = [new PlaceScoreRequest { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4 }];

            var result = await _useCase.CalculateAsync(request);
            var place = Assert.Single(Assert.Single(result.Zones).Places);

            Assert.Equal(25, place.Minutes);
            Assert.Equal(8.75, place.DistanceKm!.Value, 3);
            Assert.Equal([expectedMode], _routing.Modes.Distinct());
        }

        [Fact]
        public async Task CalculateAsync_WhenPlaceHasNoCoordinates_DoesNotCreateARoute()
        {
            AddZone("recoleta", 30);
            var request = BuildRequest();
            request.Places = [new PlaceScoreRequest { Name = "Trabajo" }];

            var result = await _useCase.CalculateAsync(request);

            Assert.Empty(Assert.Single(result.Zones).Places);
            Assert.Empty(_routing.Modes);
        }

        private sealed class FakeZoneProfilesRepository : IZoneProfilesRepository
        {
            public List<ZoneProfile> Zones { get; } = [];

            // Zones around the places; every zone when null.
            public string[]? NearbySlugs { get; set; }

            public Task<IReadOnlyList<ZoneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ZoneProfile>>(Zones);

            public Task InvalidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<IReadOnlyList<string>> ListNearbyZoneSlugsAsync(
                IReadOnlyCollection<(double Latitude, double Longitude)> locations,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(NearbySlugs ?? Zones.Select(zone => zone.Slug).ToArray());
        }

        private sealed class FakeRoutingClient : IRoutingClient
        {
            public bool IsAvailable { get; set; } = true;

            // Trips from this latitude have no route (the routing service answers without minutes).
            public double? UnreachableLatitude { get; set; }

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

                if (UnreachableLatitude == originLatitude)
                    return Task.FromResult<TravelTime?>(new TravelTime(null, false));

                return Task.FromResult<TravelTime?>(IsAvailable ? new TravelTime(25, false, 8.75) : null);
            }
        }
    }
}
