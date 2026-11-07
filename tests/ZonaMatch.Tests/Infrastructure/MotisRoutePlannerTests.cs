using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public sealed class MotisRoutePlannerTests
{
    [Fact]
    public async Task PlanAsync_WhenTransitPlanContainsRouteDetails_MapsLegsAndGeometry()
    {
        const string json = """
            {
              "itineraries": [{
                "duration": 1800,
                "transfers": 1,
                "legs": [
                  {"mode": "WALK", "duration": 300, "distance": 250, "from": {"name": "START"}, "to": {"name": "First stop"}},
                  {
                    "mode": "SUBWAY", "duration": 900, "routeShortName": "B", "displayName": "Line B",
                    "headsign": "Juan Manuel de Rosas", "agencyName": "Subte", "routeColor": "#123456",
                    "from": {"name": "First stop"}, "to": {"name": "Second stop"},
                    "legGeometry": {"points": "??_ibE?", "precision": 7}
                  },
                  {"mode": "WALK", "duration": 600, "distance": 400, "from": {"name": "Second stop"}, "to": {"name": "END"}}
                ]
              }],
              "direct": []
            }
            """;
        using var harness = CreateHarness(json);

        var route = await harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.PublicTransit);

        route.Should().NotBeNull();
        route!.DurationSeconds.Should().Be(1800);
        route.Legs.Should().HaveCount(3);
        route.Legs[0].Mode.Should().Be("walk");
        route.Legs[0].FromName.Should().BeNull("MOTIS uses START for the trip origin");
        route.Legs[0].ToName.Should().Be("First stop");
        route.Legs[1].Mode.Should().Be("subway");
        route.Legs[1].LineName.Should().Be("Line B");
        route.Legs[1].Headsign.Should().Be("Juan Manuel de Rosas");
        route.Legs[1].AgencyName.Should().Be("Subte");
        route.Legs[1].Color.Should().Be("#123456");
        route.Legs[1].Path.Should().HaveCount(2);
        route.Legs[2].ToName.Should().BeNull("MOTIS uses END for the trip destination");
    }

    [Fact]
    public async Task PlanAsync_WhenTransferPenaltyMakesTransitSlower_SelectsLongerDirectItinerary()
    {
        const string json = """
            {
              "itineraries": [{
                "duration": 900,
                "transfers": 1,
                "legs": [{"mode": "SUBWAY", "duration": 900, "routeShortName": "B"}]
              }],
              "direct": [{
                "duration": 1100,
                "transfers": 0,
                "legs": [{"mode": "CAR", "duration": 1100, "distance": 2200}]
              }]
            }
            """;
        using var harness = CreateHarness(json);

        var route = await harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.PublicTransit);

        route.Should().NotBeNull();
        route!.DurationSeconds.Should().Be(1100);
        route.Legs.Should().ContainSingle();
        route.Legs[0].Mode.Should().Be("car");
    }

    [Fact]
    public async Task PlanAsync_WhenTransitLegIsShort_SelectsLongerWalkingItinerary()
    {
        const string json = """
            {
              "itineraries": [{
                "duration": 600,
                "transfers": 0,
                "legs": [{
                  "mode": "BUS", "duration": 600, "routeShortName": "60",
                  "legGeometry": {"points": "????", "precision": 5}
                }]
              }],
              "direct": [{
                "duration": 1000,
                "transfers": 0,
                "legs": [{"mode": "WALK", "duration": 1000, "distance": 900}]
              }]
            }
            """;
        using var harness = CreateHarness(json);

        var route = await harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.PublicTransit);

        route.Should().NotBeNull();
        route!.DurationSeconds.Should().Be(1000);
        route.Legs.Should().ContainSingle();
        route.Legs[0].Mode.Should().Be("walk");
    }

    [Theory]
    [InlineData(TravelMode.Car, "transitModes=&directModes=CAR")]
    [InlineData(TravelMode.Bicycle, "transitModes=&directModes=BIKE&maxDirectTime=7200")]
    [InlineData(TravelMode.Walking, "transitModes=&directModes=WALK&maxDirectTime=7200")]
    [InlineData(TravelMode.PublicTransit, "transitModes=TRANSIT&directModes=WALK")]
    public async Task PlanAsync_WhenTravelModeIsSelected_SendsModeAndConfiguredReferenceTime(
        TravelMode mode,
        string expectedModeParameters)
    {
        const string referenceTime = "2026-05-12T11:00:00Z";
        using var harness = CreateHarness(
            """{"direct":[{"duration":600,"transfers":0,"legs":[{"mode":"WALK","duration":600,"distance":500}]}]}""",
            referenceTime: referenceTime);

        await harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, mode);

        var query = Uri.UnescapeDataString(harness.Handler.LastRequestUri!.Query);
        query.Should().Contain("fromPlace=-34.6,-58.4");
        query.Should().Contain("toPlace=-34.5,-58.3");
        query.Should().Contain($"time={referenceTime}");
        query.Should().Contain(expectedModeParameters);
    }

    [Fact]
    public async Task PlanAsync_WhenRouteIsCached_DoesNotRequestMotisAgain()
    {
        using var harness = CreateHarness(
            """{"direct":[{"duration":600,"transfers":0,"legs":[{"mode":"WALK","duration":600,"distance":500}]}]}""");

        var firstResult = await harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Walking);
        var secondResult = await harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Walking);

        firstResult.Should().NotBeNull();
        secondResult.Should().BeEquivalentTo(firstResult);
        harness.Handler.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task PlanAsync_WhenMotisReturnsAnErrorStatus_ThrowsServiceUnavailable(HttpStatusCode statusCode)
    {
        using var harness = CreateHarness("{}", statusCode: statusCode);

        var act = () => harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Car);

        await act.Should().ThrowAsync<ServiceUnavailableException>()
            .WithMessage("El servicio de recorridos no está disponible.");
    }

    [Fact]
    public async Task PlanAsync_WhenHttpRequestFails_ThrowsServiceUnavailable()
    {
        using var harness = CreateHarness("{}", httpException: new HttpRequestException("MOTIS is unreachable."));

        var act = () => harness.Planner.PlanAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Car);

        await act.Should().ThrowAsync<ServiceUnavailableException>()
            .WithMessage("El servicio de recorridos no está disponible.");
    }

    private static PlannerHarness CreateHarness(
        string responseJson,
        string? referenceTime = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        HttpRequestException? httpException = null)
    {
        var handler = new RecordingHttpMessageHandler(responseJson, statusCode, httpException);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://motis.test/") };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var configurationValues = new Dictionary<string, string?>();
        if (referenceTime is not null)
            configurationValues["Motis:FechaReferencia"] = referenceTime;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        return new PlannerHarness(handler, httpClient, cache, new MotisRoutePlanner(httpClient, cache, configuration));
    }

    private sealed class PlannerHarness(
        RecordingHttpMessageHandler handler,
        HttpClient httpClient,
        MemoryCache cache,
        MotisRoutePlanner planner) : IDisposable
    {
        public RecordingHttpMessageHandler Handler { get; } = handler;
        public MotisRoutePlanner Planner { get; } = planner;

        public void Dispose()
        {
            httpClient.Dispose();
            cache.Dispose();
        }
    }

    private sealed class RecordingHttpMessageHandler(
        string responseJson,
        HttpStatusCode statusCode,
        HttpRequestException? httpException) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;

            if (httpException is not null)
                return Task.FromException<HttpResponseMessage>(httpException);

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
