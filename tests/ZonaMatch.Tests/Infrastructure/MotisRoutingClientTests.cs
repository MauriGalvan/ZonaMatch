using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public class MotisRoutingClientTests
{
    [Fact]
    public async Task UsesTimeAndDistanceFromTheSameItinerary()
    {
        const string json = """
            {
              "itineraries": [{"duration": 1200, "legs": [{"distance": 1000}]}],
              "direct": [{"duration": 600, "legs": [{"distance": 3000}, {"distance": 500}]}]
            }
            """;

        var (travelTime, _) = await QueryAsync(json);

        Assert.Equal(10, travelTime!.Minutes);
        Assert.Equal(3.5, travelTime.DistanceKm!.Value, 3);
    }

    [Fact]
    public async Task AddsWalkingSegmentsAndTransitGeometry()
    {
        const string json = """
            {
              "itineraries": [{
                "duration": 1800,
                "legs": [
                  {"distance": 300},
                  {"legGeometry": {"points": "??_ibE?", "precision": 5, "length": 2}},
                  {"distance": 200}
                ]
              }],
              "direct": []
            }
            """;

        var (travelTime, _) = await QueryAsync(json, TravelMode.PublicTransit);

        Assert.Equal(30, travelTime!.Minutes);
        Assert.InRange(travelTime.DistanceKm!.Value, 111.69, 111.70);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"distance\": -1}")]
    [InlineData("{\"legGeometry\": {\"points\": \"_\", \"precision\": 7}}")]
    public async Task DoesNotReturnPartialDistanceWhenALegCannotBeMeasured(string legJson)
    {
        var json = $$"""
            {"itineraries": [{"duration": 1800, "legs": [{"distance": 300}, {{legJson}}]}]}
            """;

        var (travelTime, _) = await QueryAsync(json);

        Assert.Equal(30, travelTime!.Minutes);
        Assert.Null(travelTime.DistanceKm);
    }

    [Theory]
    [InlineData("{\"itineraries\":[],\"direct\":[]}")]
    [InlineData("{\"direct\":[{\"legs\":[{\"distance\":1000}]}]}")]
    public async Task ReturnsNoTimeOrDistanceWhenNoItineraryHasDuration(string json)
    {
        var (travelTime, _) = await QueryAsync(json);

        Assert.NotNull(travelTime);
        Assert.Null(travelTime.Minutes);
        Assert.Null(travelTime.DistanceKm);
    }

    [Fact]
    public async Task AcceptsAZeroMeterRoute()
    {
        var (travelTime, _) = await QueryAsync(
            """{"direct":[{"duration":0,"legs":[{"distance":0}]}]}""");

        Assert.Equal(0, travelTime!.Minutes);
        Assert.Equal(0.0, travelTime.DistanceKm!.Value);
    }

    [Theory]
    [InlineData(TravelMode.Car, "directModes=CAR")]
    [InlineData(TravelMode.Bicycle, "directModes=BIKE")]
    [InlineData(TravelMode.Walking, "directModes=WALK")]
    [InlineData(TravelMode.PublicTransit, "transitModes=TRANSIT")]
    public async Task RequestsTheSelectedTravelMode(TravelMode mode, string parameter)
    {
        var (_, query) = await QueryAsync(
            """{"direct":[{"duration":600,"legs":[{"distance":1000}]}]}""", mode);

        Assert.Contains(parameter, query);
        Assert.Contains("detailedLegs=true", query);
        Assert.Contains("detailedTransfers=true", query);
        Assert.Contains("useRoutedTransfers=true", query);
    }

    [Theory]
    [InlineData("{\"error\":\"sin servicio\"}")]
    [InlineData("{\"error\":{\"message\":\"sin servicio\"}}")]
    [InlineData("JSON invalido")]
    public async Task InvalidResponseOrErrorAllowsTheExistingEstimateToBeUsed(string json)
    {
        var (travelTime, _) = await QueryAsync(json);

        Assert.Null(travelTime);
    }

    private static async Task<(TravelTime? TravelTime, string Query)> QueryAsync(
        string json, TravelMode mode = TravelMode.Car)
    {
        using var handler = new JsonHandler(json);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://motis.test/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new MotisRoutingClient(
            http, cache, TimeProvider.System, new ConfigurationBuilder().Build(),
            NullLogger<MotisRoutingClient>.Instance);

        var travelTime = await client.GetTravelTimeAsync(-34.6, -58.4, -34.5, -58.3, mode);
        return (travelTime, handler.Query);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        public string Query { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Query = request.RequestUri!.Query;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
