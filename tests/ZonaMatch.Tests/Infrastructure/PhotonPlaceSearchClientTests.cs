using System.Net;
using System.Text;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure
{
    public class PhotonPlaceSearchClientTests
    {
        private const string Json = """
            {
              "type": "FeatureCollection",
              "features": [
                { "geometry": { "type": "Point", "coordinates": [-58.5, -34.6] },
                  "properties": { "osm_type": "N", "osm_id": 1, "type": "house", "housenumber": "666", "street": "Gutenberg", "city": "Vicente Lopez" } },
                { "geometry": { "type": "Point", "coordinates": [-58.51, -34.61] },
                  "properties": { "osm_type": "W", "osm_id": 2, "type": "street", "name": "Maipu", "city": "Olivos" } },
                { "geometry": { "type": "Point", "coordinates": [-58.52, -34.62] },
                  "properties": { "osm_type": "W", "osm_id": 3, "type": "street", "name": "Maipu", "city": "Olivos" } },
                { "geometry": { "type": "Point", "coordinates": [-58.53, -34.63] },
                  "properties": { "osm_type": "R", "osm_id": 4, "type": "district", "name": "Florida", "city": "Vicente Lopez" } },
                { "geometry": { "type": "Point", "coordinates": [-58.54, -34.64] },
                  "properties": { "osm_type": "N", "osm_id": 5, "type": "house", "name": "UNLaM", "city": "San Justo" } },
                { "geometry": { "type": "Point", "coordinates": [-58.55, -34.65] },
                  "properties": { "osm_type": "N", "osm_id": 6, "type": "house" } }
              ]
            }
            """;

        [Fact]
        public async Task SearchAsync_MapsGeoJsonFeaturesToPlaces()
        {
            var handler = new StubHandler(HttpStatusCode.OK, Json);
            var client = CreateClient(handler);

            var results = await client.SearchAsync("gutenberg", 10);

            Assert.Equal(["Dirección", "Calle", "Zona", "Lugar"], results.Select(result => result.Kind));
            Assert.Equal("666 Gutenberg", results[0].Name);
            Assert.Equal("Vicente Lopez", results[0].Locality);
            Assert.Equal(-34.6, results[0].Latitude);
            Assert.Equal(-58.5, results[0].Longitude);
            Assert.Equal("photon:N1", results[0].Id);
        }

        [Fact]
        public async Task SearchAsync_DropsRepeatedStreetsInTheSameLocalityAndFeaturesWithoutTitle()
        {
            var results = await CreateClient(new StubHandler(HttpStatusCode.OK, Json)).SearchAsync("maipu", 10);

            Assert.Single(results, result => result.Name == "Maipu");
            Assert.DoesNotContain(results, result => result.Id == "photon:N6");
        }

        [Fact]
        public async Task SearchAsync_SendsTheBoundingBoxAndTheEscapedQuery()
        {
            var handler = new StubHandler(HttpStatusCode.OK, Json);

            await CreateClient(handler).SearchAsync("Av. Maipú 4493", 8);

            var uri = Assert.Single(handler.Requests).RequestUri!.AbsoluteUri;
            Assert.Contains("api?", uri);
            Assert.Contains("bbox=-58.99,-34.93,-58.01,-34.33", uri);
            Assert.Contains("limit=24", uri);
            Assert.Contains("q=Av.%20Maip%C3%BA%204493", uri);
            Assert.DoesNotContain("lang=", uri);
        }

        [Fact]
        public async Task SearchAsync_WhenPhotonIsDown_ThrowsServiceUnavailable()
        {
            var client = CreateClient(new StubHandler(HttpStatusCode.ServiceUnavailable, string.Empty));

            await Assert.ThrowsAsync<ServiceUnavailableException>(() => client.SearchAsync("maipu", 8));
        }

        [Fact]
        public void UsesQueryVariants_IsFalse()
        {
            Assert.False(CreateClient(new StubHandler(HttpStatusCode.OK, Json)).UsesQueryVariants);
        }

        private const string AddressesJson = """
            {
              "type": "FeatureCollection",
              "features": [
                { "geometry": { "type": "Point", "coordinates": [-58.5, -34.6] },
                  "properties": { "osm_type": "N", "osm_id": 1, "type": "house", "housenumber": "1200", "street": "Avenida General Paz", "city": "Buenos Aires" } },
                { "geometry": { "type": "Point", "coordinates": [-58.4, -34.7] },
                  "properties": { "osm_type": "N", "osm_id": 2, "type": "house", "housenumber": "1200", "street": "Avenida 9 de Julio", "city": "Lanús Este" } },
                { "geometry": { "type": "Point", "coordinates": [-58.5, -34.5] },
                  "properties": { "osm_type": "N", "osm_id": 3, "type": "house", "housenumber": "4493", "street": "Maipú", "city": "Olivos" } }
              ]
            }
            """;

        [Fact]
        public async Task SearchAsync_WithAHouseNumber_DropsAddressesOnOtherStreets()
        {
            var results = await CreateClient(new StubHandler(HttpStatusCode.OK, AddressesJson)).SearchAsync("avenida general paz 1200", 10);

            Assert.Equal("1200 Avenida General Paz", Assert.Single(results).Name);
        }

        [Fact]
        public async Task SearchAsync_WithAHouseNumber_IgnoresAccentsAndAlsoMatchesThePlace()
        {
            var results = await CreateClient(new StubHandler(HttpStatusCode.OK, AddressesJson)).SearchAsync("maipu 4493, olivos", 10);

            Assert.Equal(["4493 Maipú"], results.Select(result => result.Name));
        }

        [Fact]
        public async Task SearchAsync_WithoutAHouseNumber_KeepsEveryResult()
        {
            var results = await CreateClient(new StubHandler(HttpStatusCode.OK, AddressesJson)).SearchAsync("avenida general paz", 10);

            Assert.Equal(3, results.Count);
        }

        private static PhotonPlaceSearchClient CreateClient(StubHandler handler) =>
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
