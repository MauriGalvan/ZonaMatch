using FluentAssertions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Services
{
    public class ZoneTripTimesTests
    {
        // Ramos Mejía center; the near place is about 300 m away, the far one about 10 km
        private static readonly ZoneProfile Zone = new()
        {
            Slug = "ramos-mejia", Name = "Ramos Mejía", Level = 9, AreaKm2 = 10, Population = 1000, SchoolCount = 10,
            Latitude = -34.6400, Longitude = -58.5650,
        };

        private static readonly UserPlace NearPlace = new("Club", -34.6420, -58.5630, "Alta", 45);
        private static readonly UserPlace FarPlace = new("Trabajo", -34.6037, -58.4700, "Alta", 45);

        private readonly RecordingRoutingClient _routing = new();

        [Fact]
        public async Task MeasureAsync_WhenPlaceIsWithinWalkingDistance_WalksInsteadOfTakingPublicTransit()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);

            await ZoneTripTimes.MeasureAsync(_routing, [Zone], [NearPlace, FarPlace], TravelMode.PublicTransit, limiter, CancellationToken.None);

            _routing.Requests.Should().ContainSingle(request => request.Mode == TravelMode.Walking && request.DestinationLatitude == NearPlace.Latitude);
            _routing.Requests.Should().ContainSingle(request => request.Mode == TravelMode.PublicTransit && request.DestinationLatitude == FarPlace.Latitude);
        }

        [Fact]
        public async Task MeasureAsync_WhenZoneHasAStartingPoint_MeasuresFromIt()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);
            var origins = new Dictionary<string, (double Latitude, double Longitude)> { [Zone.Slug] = (-34.6500, -58.5700) };

            await ZoneTripTimes.MeasureAsync(_routing, [Zone], [FarPlace], TravelMode.PublicTransit, limiter, CancellationToken.None, origins);

            _routing.Requests.Should().ContainSingle()
                .Which.Should().Match<RoutingRequest>(request => request.OriginLatitude == -34.6500 && request.OriginLongitude == -58.5700);
        }

        [Fact]
        public async Task MeasureAsync_WhenRoutingHasNoTrip_EstimatesTheTime()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);
            _routing.Answer = null;

            var times = await ZoneTripTimes.MeasureAsync(_routing, [Zone], [FarPlace], TravelMode.PublicTransit, limiter, CancellationToken.None);

            times[Zone.Slug].Single()!.IsEstimated.Should().BeTrue();
        }

        [Fact]
        public async Task MeasureAsync_WhenPlaceHasNoLocation_ReturnsNoTimeWithoutCallingRouting()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);
            var placeWithoutLocation = new UserPlace("Sin dirección", null, null, "Alta", 45);

            var times = await ZoneTripTimes.MeasureAsync(_routing, [Zone], [placeWithoutLocation], TravelMode.PublicTransit, limiter, CancellationToken.None);

            times[Zone.Slug].Single().Should().BeNull();
            _routing.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task MeasureAsync_WhenHouseholdHasNoBicycle_DoesNotMeasureByBike()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);

            await ZoneTripTimes.MeasureAsync(_routing, [Zone], [FarPlace], TravelMode.PublicTransit, limiter, CancellationToken.None);

            _routing.Requests.Should().NotContain(request => request.Mode == TravelMode.Bicycle);
        }

        [Fact]
        public async Task MeasureAsync_WhenBikeIsFasterForAHouseholdWithABike_KeepsTheBikeTrip()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);
            _routing.AnswerByMode[TravelMode.Bicycle] = new TravelTime(20, false, 6);

            var times = await ZoneTripTimes.MeasureAsync(
                _routing, [Zone], [FarPlace], TravelMode.PublicTransit, limiter, CancellationToken.None, withBicycle: true);

            times[Zone.Slug].Single().Should().Be(new TravelTime(20, false, 6, ByBicycle: true));
        }

        [Fact]
        public async Task MeasureAsync_WhenBikeIsSlowerForAHouseholdWithABike_KeepsTheMainTrip()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);
            _routing.AnswerByMode[TravelMode.Bicycle] = new TravelTime(50, false);

            var times = await ZoneTripTimes.MeasureAsync(
                _routing, [Zone], [FarPlace], TravelMode.PublicTransit, limiter, CancellationToken.None, withBicycle: true);

            times[Zone.Slug].Single().Should().Be(new TravelTime(30, false));
        }

        [Fact]
        public async Task MeasureAsync_WhenOnlyTheBikeHasARoute_KeepsTheBikeTrip()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);
            _routing.Answer = new TravelTime(null, false);
            _routing.AnswerByMode[TravelMode.Bicycle] = new TravelTime(41, false);

            var times = await ZoneTripTimes.MeasureAsync(
                _routing, [Zone], [FarPlace], TravelMode.PublicTransit, limiter, CancellationToken.None, withBicycle: true);

            times[Zone.Slug].Single()!.ByBicycle.Should().BeTrue();
            times[Zone.Slug].Single()!.Minutes.Should().Be(41);
        }

        [Fact]
        public async Task MeasureAsync_WhenModeIsBicycle_MarksTheTripAsByBike()
        {
            using var limiter = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);

            var times = await ZoneTripTimes.MeasureAsync(
                _routing, [Zone], [FarPlace], TravelMode.Bicycle, limiter, CancellationToken.None, withBicycle: true);

            _routing.Requests.Should().ContainSingle();
            times[Zone.Slug].Single()!.ByBicycle.Should().BeTrue();
        }

        private sealed record RoutingRequest(
            double OriginLatitude, double OriginLongitude, double DestinationLatitude, double DestinationLongitude, TravelMode Mode);

        private sealed class RecordingRoutingClient : IRoutingClient
        {
            public List<RoutingRequest> Requests { get; } = [];
            public TravelTime? Answer { get; set; } = new(30, false);

            // Answers for specific modes; the rest get Answer.
            public Dictionary<TravelMode, TravelTime?> AnswerByMode { get; } = [];

            public Task<TravelTime?> GetTravelTimeAsync(
                double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude, TravelMode mode,
                CancellationToken cancellationToken = default)
            {
                lock (Requests)
                    Requests.Add(new RoutingRequest(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode));
                return Task.FromResult(AnswerByMode.TryGetValue(mode, out var answer) ? answer : Answer);
            }
        }
    }
}
