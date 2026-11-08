using FluentAssertions;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public class MotisRoutingClientTests
{
    [Fact]
    public async Task GetTravelTimeAsync_WhenMultipleItinerariesExist_UsesTimeAndDistanceFromTheSameItinerary()
    {
        const string json = """
            {
              "itineraries": [{"duration": 1200, "legs": [{"distance": 1000}]}],
              "direct": [{"duration": 600, "legs": [{"distance": 3000}, {"distance": 500}]}]
            }
            """;

        var (travelTime, _) = await QueryAsync(json);

        travelTime!.Minutes.Should().Be(10);
        travelTime.DistanceKm!.Value.Should().BeApproximately(3.5, 0.001);
    }

    [Fact]
    public async Task GetTravelTimeAsync_WhenTransitGeometryIsProvided_IncludesWalkingSegmentsAndGeometry()
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

        travelTime!.Minutes.Should().Be(30);
        travelTime.DistanceKm!.Value.Should().BeInRange(111.69, 111.70);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"distance\": -1}")]
    [InlineData("{\"legGeometry\": {\"points\": \"_\", \"precision\": 7}}")]
    public async Task GetTravelTimeAsync_WhenALegCannotBeMeasured_ReturnsTimeWithoutPartialDistance(string legJson)
    {
        var json = $$"""
            {"itineraries": [{"duration": 1800, "legs": [{"distance": 300}, {{legJson}}]}]}
            """;

        var (travelTime, _) = await QueryAsync(json);

        travelTime!.Minutes.Should().Be(30);
        travelTime.DistanceKm.Should().BeNull();
    }

    [Theory]
    [InlineData("{\"itineraries\":[],\"direct\":[]}")]
    [InlineData("{\"direct\":[{\"legs\":[{\"distance\":1000}]}]}")]
    public async Task GetTravelTimeAsync_WhenNoItineraryHasDuration_ReturnsNoTimeOrDistance(string json)
    {
        var (travelTime, _) = await QueryAsync(json);

        travelTime.Should().NotBeNull();
        travelTime!.Minutes.Should().BeNull();
        travelTime.DistanceKm.Should().BeNull();
    }

    [Fact]
    public async Task GetTravelTimeAsync_WhenRouteHasZeroMeters_AcceptsZeroTimeAndDistance()
    {
        var (travelTime, _) = await QueryAsync(
            """{"direct":[{"duration":0,"legs":[{"distance":0}]}]}""");

        travelTime!.Minutes.Should().Be(0);
        travelTime.DistanceKm!.Value.Should().Be(0.0);
    }

    [Theory]
    [InlineData(TravelMode.Car, "directModes=CAR")]
    [InlineData(TravelMode.Bicycle, "directModes=BIKE")]
    [InlineData(TravelMode.Walking, "directModes=WALK")]
    [InlineData(TravelMode.PublicTransit, "transitModes=TRANSIT")]
    [InlineData(TravelMode.PublicTransit, "maxPreTransitTime=7200&maxPostTransitTime=7200")]
    [InlineData(TravelMode.PublicTransit, "directModes=WALK&maxDirectTime=7200")]
    public async Task GetTravelTimeAsync_WhenTravelModeIsSelected_UsesItsRequestParameters(TravelMode mode, string parameter)
    {
        var (_, query) = await QueryAsync(
            """{"direct":[{"duration":600,"legs":[{"distance":1000}]}]}""", mode);

        query.Should().Contain(parameter);
        query.Should().Contain("detailedLegs=true");
        query.Should().Contain("detailedTransfers=true");
        query.Should().Contain("useRoutedTransfers=true");
    }

    [Theory]
    [InlineData("{\"error\":\"sin servicio\"}")]
    [InlineData("{\"error\":{\"message\":\"sin servicio\"}}")]
    [InlineData("JSON invalido")]
    public async Task GetTravelTimeAsync_WhenResponseIsInvalidOrContainsAnError_ReturnsNoTravelTime(string json)
    {
        var (travelTime, _) = await QueryAsync(json);

        travelTime.Should().BeNull();
    }

    [Fact]
    public async Task GetTravelTimeAsync_WhenTheSameTripIsAskedAgain_AnswersFromTheCache()
    {
        using var handler = new SequenceHandler("""{"direct": [{"duration": 600, "legs": [{"distance": 1000}]}]}""");
        var client = CreateClient(handler);

        await client.GetTravelTimeAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Car);
        var second = await client.GetTravelTimeAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Car);

        second!.Minutes.Should().Be(10);
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task GetTravelTimeAsync_WhenMotisAnswersWithAnError_DoesNotCacheIt()
    {
        using var handler = new SequenceHandler(
            """{"error": "no route"}""",
            """{"direct": [{"duration": 600, "legs": [{"distance": 1000}]}]}""");
        var client = CreateClient(handler);

        var first = await client.GetTravelTimeAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Car);
        var second = await client.GetTravelTimeAsync(-34.6, -58.4, -34.5, -58.3, TravelMode.Car);

        first.Should().BeNull();
        second!.Minutes.Should().Be(10);
        handler.Requests.Should().Be(2);
    }

    private static MotisRoutingClient CreateClient(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://motis.test/") },
        HybridCacheTestSupport.Create(), TimeProvider.System, new ConfigurationBuilder().Build(),
        NullLogger<MotisRoutingClient>.Instance);

    // Answers each request with the next JSON of the list (the last one repeats) and counts the requests
    private sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = responses[Math.Min(Requests, responses.Length - 1)];
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static async Task<(TravelTime? TravelTime, string Query)> QueryAsync(
        string json, TravelMode mode = TravelMode.Car)
    {
        using var handler = new JsonHandler(json);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://motis.test/") };
        var cache = HybridCacheTestSupport.Create();
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
