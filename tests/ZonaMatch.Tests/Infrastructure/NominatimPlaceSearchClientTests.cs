using System.Net;
using FluentAssertions;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public class NominatimPlaceSearchClientTests
{
    [Fact]
    public async Task SearchAsync_WhenUsingAmbaBounds_FormatsTheViewboxInNominatimOrder()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://nominatim.test/") };
        var client = new NominatimPlaceSearchClient(httpClient, PlaceSearchBounds.LoadAmba());

        await client.SearchAsync("Maipú", 8);

        handler.RequestUri!.Query.Should().Contain("viewbox=-58.99,-34.33,-58.01,-34.93&bounded=1");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }
}
