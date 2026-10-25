using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Domain.Puntaje;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public class MotisRuteoClientTests
{
    [Fact]
    public async Task Usa_tiempo_y_distancia_del_mismo_itinerario()
    {
        const string json = """
            {
              "itineraries": [{"duration": 1200, "legs": [{"distance": 1000}]}],
              "direct": [{"duration": 600, "legs": [{"distance": 3000}, {"distance": 500}]}]
            }
            """;

        var (viaje, _) = await ConsultarAsync(json);

        Assert.Equal(10, viaje!.Minutos);
        Assert.Equal(3.5, viaje.DistanciaKm!.Value, 3);
    }

    [Fact]
    public async Task Suma_caminatas_y_geometria_del_transporte()
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

        var (viaje, _) = await ConsultarAsync(json, ModoViaje.TransportePublico);

        Assert.Equal(30, viaje!.Minutos);
        Assert.InRange(viaje.DistanciaKm!.Value, 111.69, 111.70);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"distance\": -1}")]
    [InlineData("{\"legGeometry\": {\"points\": \"_\", \"precision\": 7}}")]
    public async Task No_devuelve_total_parcial_si_un_tramo_no_se_puede_medir(string tramo)
    {
        var json = $$"""
            {"itineraries": [{"duration": 1800, "legs": [{"distance": 300}, {{tramo}}]}]}
            """;

        var (viaje, _) = await ConsultarAsync(json);

        Assert.Equal(30, viaje!.Minutos);
        Assert.Null(viaje.DistanciaKm);
    }

    [Theory]
    [InlineData("{\"itineraries\":[],\"direct\":[]}")]
    [InlineData("{\"direct\":[{\"legs\":[{\"distance\":1000}]}]}")]
    public async Task Sin_itinerario_con_duracion_no_hay_tiempo_ni_distancia(string json)
    {
        var (viaje, _) = await ConsultarAsync(json);

        Assert.NotNull(viaje);
        Assert.Null(viaje.Minutos);
        Assert.Null(viaje.DistanciaKm);
    }

    [Fact]
    public async Task Acepta_un_recorrido_de_cero_metros()
    {
        var (viaje, _) = await ConsultarAsync(
            """{"direct":[{"duration":0,"legs":[{"distance":0}]}]}""");

        Assert.Equal(0, viaje!.Minutos);
        Assert.Equal(0.0, viaje.DistanciaKm!.Value);
    }

    [Theory]
    [InlineData(ModoViaje.Auto, "directModes=CAR")]
    [InlineData(ModoViaje.Bici, "directModes=BIKE")]
    [InlineData(ModoViaje.APie, "directModes=WALK")]
    [InlineData(ModoViaje.TransportePublico, "transitModes=TRANSIT")]
    public async Task Consulta_el_medio_de_transporte_elegido(ModoViaje modo, string parametro)
    {
        var (_, query) = await ConsultarAsync(
            """{"direct":[{"duration":600,"legs":[{"distance":1000}]}]}""", modo);

        Assert.Contains(parametro, query);
        Assert.Contains("detailedLegs=true", query);
        Assert.Contains("detailedTransfers=true", query);
        Assert.Contains("useRoutedTransfers=true", query);
    }

    [Theory]
    [InlineData("{\"error\":\"sin servicio\"}")]
    [InlineData("{\"error\":{\"message\":\"sin servicio\"}}")]
    [InlineData("JSON invalido")]
    public async Task Respuesta_invalida_o_error_permite_usar_la_estimacion_existente(string json)
    {
        var (viaje, _) = await ConsultarAsync(json);

        Assert.Null(viaje);
    }

    private static async Task<(TiempoViaje? Viaje, string Query)> ConsultarAsync(
        string json, ModoViaje modo = ModoViaje.Auto)
    {
        using var handler = new JsonHandler(json);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://motis.test/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cliente = new MotisRuteoClient(
            http, cache, TimeProvider.System, new ConfigurationBuilder().Build(),
            NullLogger<MotisRuteoClient>.Instance);

        var viaje = await cliente.CalcularAsync(-34.6, -58.4, -34.5, -58.3, modo);
        return (viaje, handler.Query);
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
