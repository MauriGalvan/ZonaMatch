using ZonaMatch.Application.DTOs.Routes.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Routes;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.UseCases.Routes
{
    public class GetRouteTests
    {
        private readonly FakeRoutePlanner _planner = new();

        private static RouteRequest Request(string? mode = null) => new()
        {
            FromLatitude = -34.6532,
            FromLongitude = -58.6195,
            ToLatitude = -34.6037,
            ToLongitude = -58.3816,
            Mode = mode,
        };

        [Fact]
        public async Task ExecuteAsync_WhenTransitTripExists_ReturnsLegsWithLinesInMinutes()
        {
            _planner.Plan = new RoutePlan(3540,
            [
                new RouteLeg("walk", 170, 598.4, null, "Morón", null, null, null, null, [new(-34.65, -58.62), new(-34.651, -58.619)]),
                new RouteLeg("train", 2340, null, "Morón", "Once", "Sarmiento", "Once", "Trenes Argentinos", null, [new(-34.651, -58.619), new(-34.608, -58.408)]),
                new RouteLeg("subway", 300, null, "Pueyrredón", "Carlos Pellegrini", "B", "Leandro N. Alem", "Subte", "ff0000", []),
            ]);

            var response = await new GetRoute(_planner).ExecuteAsync(Request());

            Assert.Equal("transporte-publico", response.Mode);
            Assert.Equal(59, response.DurationMinutes);
            Assert.Equal(ModoViaje.TransportePublico, _planner.RequestedMode);
            Assert.Collection(response.Legs,
                walk =>
                {
                    Assert.Equal("walk", walk.Mode);
                    Assert.Equal(3, walk.DurationMinutes);
                    Assert.Equal(598, walk.DistanceMeters);
                    Assert.Equal(new[] { -34.65, -58.62 }, walk.Path[0]);
                },
                train =>
                {
                    Assert.Equal("Sarmiento", train.Line);
                    Assert.Equal("Once", train.Headsign);
                    Assert.Equal(39, train.DurationMinutes);
                },
                subway => Assert.Equal("ff0000", subway.Color));
        }

        [Fact]
        public async Task ExecuteAsync_WhenModeIsBike_AsksTheBikeTrip()
        {
            _planner.Plan = new RoutePlan(1200, [new RouteLeg("bike", 1200, 4100, null, null, null, null, null, null, [])]);

            var response = await new GetRoute(_planner).ExecuteAsync(Request("bici"));

            Assert.Equal(ModoViaje.Bici, _planner.RequestedMode);
            Assert.Equal("bici", response.Mode);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPlaceIsTenBlocksAwayOrLessByPublicTransport_Walks()
        {
            _planner.Plan = new RoutePlan(600, [new RouteLeg("walk", 600, 800, null, null, null, null, null, null, [])]);
            var request = Request("transporte-publico");
            request.ToLatitude = request.FromLatitude + 0.007; // about 780 m north
            request.ToLongitude = request.FromLongitude;

            var response = await new GetRoute(_planner).ExecuteAsync(request);

            Assert.Equal(ModoViaje.APie, _planner.RequestedMode);
            Assert.Equal("a-pie", response.Mode);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPlaceIsTenBlocksAwayOrLessByCar_KeepsTheCar()
        {
            _planner.Plan = new RoutePlan(240, [new RouteLeg("car", 240, 800, null, null, null, null, null, null, [])]);
            var request = Request("auto");
            request.ToLatitude = request.FromLatitude + 0.007;
            request.ToLongitude = request.FromLongitude;

            var response = await new GetRoute(_planner).ExecuteAsync(request);

            Assert.Equal(ModoViaje.Auto, _planner.RequestedMode);
            Assert.Equal("auto", response.Mode);
        }

        [Fact]
        public async Task ExecuteAsync_WhenModeIsUnknown_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => new GetRoute(_planner).ExecuteAsync(Request("teletransporte")));
        }

        [Fact]
        public async Task ExecuteAsync_WhenCoordinatesAreMissing_ThrowsBadRequestException()
        {
            var request = Request();
            request.ToLatitude = 0;
            request.ToLongitude = 0;

            await Assert.ThrowsAsync<BadRequestException>(() => new GetRoute(_planner).ExecuteAsync(request));
        }

        [Fact]
        public async Task ExecuteAsync_WhenThereIsNoTrip_ThrowsNotFoundException()
        {
            _planner.Plan = null;

            await Assert.ThrowsAsync<NotFoundException>(() => new GetRoute(_planner).ExecuteAsync(Request("a-pie")));
        }

        private sealed class FakeRoutePlanner : IRoutePlanner
        {
            public RoutePlan? Plan { get; set; }
            public ModoViaje? RequestedMode { get; private set; }

            public Task<RoutePlan?> PlanAsync(
                double fromLatitude, double fromLongitude, double toLatitude, double toLongitude, ModoViaje mode,
                CancellationToken cancellationToken = default)
            {
                RequestedMode = mode;
                return Task.FromResult(Plan);
            }
        }
    }
}
