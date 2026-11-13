using System.Net;
using System.Text;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure
{
    public class PhotonGeocodingClientTests
    {
        private const double Latitude = -34.658129276595744;
        private const double Longitude = -58.579479797872345;

        private const string HouseJson = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"osm_type":"W","osm_id":420837504,
            "type":"house","housenumber":"666","street":"Gutenberg","locality":"Marina","city":"Villa Luzuriaga",
            "county":"Partido de La Matanza","postcode":"B1704FLD"},
            "geometry":{"type":"Point","coordinates":[-58.579479797872345,-34.658129276595744]}}]}
            """;

        [Fact]
        public async Task ReverseAsync_MapsAnAddressWithTheTownAsLocality()
        {
            var address = await CreateClient(new StubHandler(HttpStatusCode.OK, HouseJson)).ReverseAsync(Latitude, Longitude);

            Assert.NotNull(address);
            Assert.Null(address.Name);
            Assert.Equal("Gutenberg", address.Street);
            Assert.Equal("666", address.StreetNumber);
            Assert.Equal("Villa Luzuriaga", address.Locality);
            Assert.Equal("B1704FLD", address.PostalCode);
            Assert.Equal("Gutenberg 666, Villa Luzuriaga", address.FullAddress);
        }

        [Fact]
        public async Task ReverseAsync_InTheCityOfBuenosAires_UsesTheBarrioAsLocality()
        {
            const string json = """
                {"features":[{"properties":{"type":"house","name":"Hospital X","street":"Calle Y","housenumber":"10",
                "locality":"Parque Avellaneda","city":"Buenos Aires","district":"Comuna 9"},
                "geometry":{"type":"Point","coordinates":[-58.579479797872345,-34.658129276595744]}}]}
                """;

            var address = await CreateClient(new StubHandler(HttpStatusCode.OK, json)).ReverseAsync(Latitude, Longitude);

            Assert.Equal("Hospital X", address!.Name);
            Assert.Equal("Parque Avellaneda", address.Locality);
        }

        [Fact]
        public async Task ReverseAsync_ForAStreetResult_FillsOnlyTheStreet()
        {
            const string json = """
                {"features":[{"properties":{"type":"street","name":"Camacuá","city":"Villa Luzuriaga"},
                "geometry":{"type":"Point","coordinates":[-58.57948,-34.65813]}}]}
                """;

            var address = await CreateClient(new StubHandler(HttpStatusCode.OK, json)).ReverseAsync(Latitude, Longitude);

            Assert.Equal("Camacuá", address!.Street);
            Assert.Null(address.StreetNumber);
            Assert.Null(address.Name);
        }

        [Fact]
        public async Task ReverseAsync_WhenTheNearestObjectIsTooFar_ReturnsNull()
        {
            // ~1.1 km north of the picked point
            const string json = """
                {"features":[{"properties":{"type":"house","street":"Gutenberg","housenumber":"1"},
                "geometry":{"type":"Point","coordinates":[-58.579479797872345,-34.648]}}]}
                """;

            Assert.Null(await CreateClient(new StubHandler(HttpStatusCode.OK, json)).ReverseAsync(Latitude, Longitude));
        }

        [Fact]
        public async Task ReverseAsync_WithoutFeatures_ReturnsNull()
        {
            var client = CreateClient(new StubHandler(HttpStatusCode.OK, """{"type":"FeatureCollection","features":[]}"""));

            Assert.Null(await client.ReverseAsync(Latitude, Longitude));
        }

        [Fact]
        public async Task ReverseAsync_SendsTheCoordinatesWithADotAndLimitOne()
        {
            var handler = new StubHandler(HttpStatusCode.OK, HouseJson);

            await CreateClient(handler).ReverseAsync(-34.5, -58.25);

            var uri = Assert.Single(handler.Requests).RequestUri!.AbsoluteUri;
            Assert.EndsWith("reverse?lat=-34.5&lon=-58.25&limit=1", uri);
        }

        [Fact]
        public async Task ReverseAsync_WhenPhotonIsDown_ThrowsHttpRequestException()
        {
            var client = CreateClient(new StubHandler(HttpStatusCode.ServiceUnavailable, string.Empty));

            await Assert.ThrowsAsync<HttpRequestException>(() => client.ReverseAsync(Latitude, Longitude));
        }

        private static PhotonGeocodingClient CreateClient(StubHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://photon.test/") }, new PhotonSearchOptions());

        private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
