using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ZonaMatch.Tests.Integration
{
    [Collection(PostgisCollection.Name)]
    public class ApiTests
    {
        private readonly PostgisFixture _fixture;
        private readonly HttpClient _client;

        public ApiTests(PostgisFixture fixture)
        {
            _fixture = fixture;
            _client = fixture.Client;
        }

        private async Task<JsonElement> GetJsonAsync(string path, HttpStatusCode expected = HttpStatusCode.OK)
        {
            var response = await _client.GetAsync(path);
            Assert.Equal(expected, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>(PostgisFixture.Json);
        }

        [Fact]
        public async Task Busca_zonas_por_nombre_y_por_partido()
        {
            var byName = await GetJsonAsync("/api/zones?query=ramos%20mej%C3%ADa");
            var byPartido = await GetJsonAsync("/api/zones?query=La%20Matanza&limit=10");
            var all = await GetJsonAsync("/api/zones");

            var ramos = Assert.Single(byName.EnumerateArray());
            Assert.Equal("ramos-mejia-la-matanza", ramos.GetProperty("slug").GetString());
            Assert.Equal("Ramos Mejía, La Matanza", ramos.GetProperty("displayName").GetString());
            Assert.Equal("localidad", ramos.GetProperty("type").GetString());
            Assert.Equal(["Ramos Mejía", "San Justo", "San Justo"], byPartido.EnumerateArray().Select(zone => zone.GetProperty("name").GetString()));
            Assert.Equal(5, all.GetArrayLength());
            await GetJsonAsync("/api/zones?limit=0", HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Detalle_de_zona_con_geojson_e_indicadores_trazables()
        {
            var luro = await GetJsonAsync("/api/zones/villa-luro-caba");
            var ramos = await GetJsonAsync("/api/zones/ramos-mejia-la-matanza");

            Assert.Equal("Comuna 10", luro.GetProperty("containerName").GetString());
            Assert.Equal("barrio", luro.GetProperty("type").GetString());
            Assert.Equal("MultiPolygon", luro.GetProperty("geometry").GetProperty("type").GetString());
            Assert.InRange(luro.GetProperty("areaKm2").GetDouble(), 8, 9);

            var population = luro.GetProperty("indicators").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "population");
            Assert.False(population.GetProperty("inherited").GetBoolean());
            Assert.Equal("Radios censales 2022", population.GetProperty("source").GetString());

            var crime = ramos.GetProperty("indicators").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "crime_rate_per_1000");
            Assert.True(crime.GetProperty("inherited").GetBoolean());
            Assert.Equal("La Matanza", crime.GetProperty("level").GetString());

            var notFound = await GetJsonAsync("/api/zones/no-existe", HttpStatusCode.NotFound);
            Assert.Equal("Recurso inexistente", notFound.GetProperty("title").GetString());
        }

        [Fact]
        public async Task Zonas_aledanas()
        {
            var neighbors = await GetJsonAsync("/api/zones/ramos-mejia-la-matanza/neighbors");

            Assert.Equal(["Haedo", "Villa Luro"], neighbors.EnumerateArray().Select(zone => zone.GetProperty("name").GetString()));
        }

        [Fact]
        public async Task Pois_en_un_radio_por_capa_sin_duplicados()
        {
            var transport = await GetJsonAsync("/api/zones/villa-luro-caba/pois?radius=1000&layers=transporte");
            var everything = await GetJsonAsync("/api/zones/ramos-mejia-la-matanza/pois?radius=2000");

            Assert.Equal(3, transport.GetArrayLength());
            Assert.All(transport.EnumerateArray(), poi => Assert.Equal("transporte", poi.GetProperty("layer").GetString()));
            Assert.Contains(transport.EnumerateArray(), poi => poi.GetProperty("source").GetString() == "OpenStreetMap");

            var names = everything.EnumerateArray().Select(poi => poi.GetProperty("name").GetString()).ToList();
            Assert.Contains("ESCUELA DE EDUCACION PRIMARIA N°39 \"EL PAMPERO\"", names);
            Assert.DoesNotContain("Escuela N° 39", names);
            var distances = everything.EnumerateArray().Select(poi => poi.GetProperty("distanceMeters").GetDouble()).ToList();
            Assert.Equal(distances.OrderBy(distance => distance), distances);

            await GetJsonAsync("/api/zones/villa-luro-caba/pois?radius=50", HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Resumen_de_capas_de_la_zona()
        {
            var summary = await GetJsonAsync("/api/zones/villa-luro-caba/summary?radius=1000");

            var layers = summary.GetProperty("layers").EnumerateArray().ToDictionary(layer => layer.GetProperty("code").GetString()!);
            Assert.Equal(7, layers.Count);
            Assert.Equal(3, layers["transporte"].GetProperty("count").GetInt32());
            Assert.Equal(1, layers["espacios_verdes"].GetProperty("count").GetInt32());
            Assert.Equal(0, layers["salud"].GetProperty("count").GetInt32());
        }

        [Fact]
        public async Task Contexto_territorial_de_un_punto()
        {
            var ramos = await GetJsonAsync("/api/territorial-context?lat=-34.65&lon=-58.565");
            var outside = await GetJsonAsync("/api/territorial-context?lat=-30&lon=-60");

            Assert.Equal("ramos-mejia-la-matanza", ramos.GetProperty("zone").GetProperty("slug").GetString());
            Assert.Equal("La Matanza", ramos.GetProperty("department").GetProperty("name").GetString());
            Assert.Equal("064270101", ramos.GetProperty("censusTract").GetProperty("code").GetString());
            Assert.Equal(JsonValueKind.Null, outside.GetProperty("zone").ValueKind);

            var invalid = await GetJsonAsync("/api/territorial-context?lat=95&lon=0", HttpStatusCode.BadRequest);
            Assert.Equal("Solicitud inválida", invalid.GetProperty("title").GetString());
        }

        [Fact]
        public async Task Catalogos_de_categorias_y_criterios()
        {
            var categories = await GetJsonAsync("/api/poi-categories");
            var criteria = await GetJsonAsync("/api/analysis/criteria");

            Assert.Equal(7, categories.GetArrayLength());
            Assert.Equal(4, categories[0].GetProperty("subcategories").GetArrayLength());
            Assert.Equal(10, criteria.GetArrayLength());
        }

        [Fact]
        public async Task Ranking_personalizado_de_punta_a_punta()
        {
            var request = new
            {
                zoneSlugs = new[] { "ramos-mejia-la-matanza", "villa-luro-caba" },
                radiusMeters = 1000,
                maxRent = 700000,
                includeSuggestion = true,
                participants = new[]
                {
                    new
                    {
                        name = "Nicolás",
                        weight = 1,
                        criteria = new[] { new { code = "seguridad", priority = "High" }, new { code = "movilidad", priority = "Medium" } },
                        points = new[] { new { name = "UNLaM", latitude = -34.67, longitude = -58.56, mode = "PublicTransport", tripsPerWeek = 3, maxMinutes = (int?)50 } },
                    },
                },
            };

            var response = await _client.PostAsJsonAsync("/api/analysis/ranking", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(PostgisFixture.Json);

            // Villa Luro gana por seguridad (dato propio) aunque Ramos Mejía queda más cerca de la UNLaM
            var zones = result.GetProperty("zones").EnumerateArray().ToList();
            Assert.Equal(["villa-luro-caba", "ramos-mejia-la-matanza"], zones.Select(zone => zone.GetProperty("slug").GetString()));
            Assert.Equal("sin_dato", zones[0].GetProperty("restrictions")[0].GetProperty("status").GetString());
            Assert.Equal("cerca_del_limite", zones[1].GetProperty("restrictions")[0].GetProperty("status").GetString());
            Assert.Equal("cumple", zones[1].GetProperty("restrictions")[1].GetProperty("status").GetString());
            Assert.Equal("dato de La Matanza · Partidos de la Provincia de Buenos Aires (2025)",
                zones[1].GetProperty("breakdown")[0].GetProperty("dataNote").GetString());
            // Haedo (aledaña) no supera a ninguna de las elegidas
            Assert.Equal(JsonValueKind.Null, result.GetProperty("suggestion").ValueKind);
        }

        [Fact]
        public async Task Ranking_con_errores_de_entrada()
        {
            var unknownZone = new
            {
                zoneSlugs = new[] { "no-existe" },
                radiusMeters = 1000,
                includeSuggestion = false,
                participants = new[] { new { name = "A", weight = 1, criteria = new[] { new { code = "educacion", priority = "Low" } }, points = Array.Empty<object>() } },
            };
            var noCriteria = new
            {
                zoneSlugs = new[] { "villa-luro-caba" },
                radiusMeters = 1000,
                includeSuggestion = false,
                participants = new[] { new { name = "A", weight = 1, criteria = Array.Empty<object>(), points = Array.Empty<object>() } },
            };

            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/analysis/ranking", unknownZone)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/analysis/ranking", noCriteria)).StatusCode);
        }

        [Fact]
        public async Task Swagger_solo_en_desarrollo_e_ingesta_deshabilitada_por_defecto()
        {
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/swagger/v1/swagger.json")).StatusCode);

            await using var production = _fixture.CreateFactory(ingestionEnabled: false, environment: "Production");
            using var client = production.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/admin/ingestion/territorial", null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/poi-categories")).StatusCode);
        }

        [Fact]
        public async Task Cors_habilitado_para_el_frontend()
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/zones");
            request.Headers.Add("Origin", "http://localhost:5173");
            request.Headers.Add("Access-Control-Request-Method", "GET");

            var response = await _client.SendAsync(request);

            Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }
    }
}
