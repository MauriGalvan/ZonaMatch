using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Infrastructure.Geo
{
    // Duracion de viajes con MOTIS (contenedor "motis" de docker-compose, GET /api/v1/plan).
    // Los horarios cargados son una semana de referencia (etl/motis/config.yml): se consulta siempre un dia habil
    // de esa semana a las 8 de la mañana. Cada viaje se guarda en memoria un dia.
    // Si MOTIS no responde, se deja de consultar un minuto para no demorar cada pedido con el timeout.
    public class MotisRuteoClient : IRuteoClient
    {
        // Martes 3 de marzo de 2026, 8:00 en Buenos Aires (UTC-3)
        private const string FechaReferenciaPorDefecto = "2026-03-03T11:00:00Z";

        private static readonly TimeSpan VigenciaViaje = TimeSpan.FromDays(1);
        private static readonly TimeSpan PausaSiFalla = TimeSpan.FromMinutes(1);

        // Hasta cuanto se busca un viaje directo a pie o en bici (segundos)
        private const int MaximoDirectoSegundos = 7200;

        private static DateTimeOffset _noConsultarHasta = DateTimeOffset.MinValue;

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly TimeProvider _reloj;
        private readonly ILogger<MotisRuteoClient> _logger;
        private readonly string _fechaReferencia;

        public MotisRuteoClient(HttpClient http, IMemoryCache cache, TimeProvider reloj, IConfiguration configuracion, ILogger<MotisRuteoClient> logger)
        {
            _http = http;
            _cache = cache;
            _reloj = reloj;
            _logger = logger;
            _fechaReferencia = configuracion["Motis:FechaReferencia"] ?? FechaReferenciaPorDefecto;
        }

        public async Task<TiempoViaje?> CalcularAsync(
            double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino, ModoViaje modo,
            CancellationToken cancellationToken = default)
        {
            // ~10 m: dos pedidos desde el mismo centro al mismo lugar comparten el resultado
            var clave = string.Create(CultureInfo.InvariantCulture,
                $"motis:{modo}:{latitudOrigen:F4},{longitudOrigen:F4}:{latitudDestino:F4},{longitudDestino:F4}");

            if (_cache.TryGetValue(clave, out TiempoViaje? guardado))
                return guardado;

            if (_reloj.GetUtcNow() < _noConsultarHasta)
                return null;

            try
            {
                var url = string.Create(CultureInfo.InvariantCulture,
                    $"api/v1/plan?fromPlace={latitudOrigen},{longitudOrigen}&toPlace={latitudDestino},{longitudDestino}&time={_fechaReferencia}&{Modos(modo)}");

                var plan = await _http.GetFromJsonAsync<Plan>(url, cancellationToken);

                if (plan is null || plan.Error is not null)
                {
                    _logger.LogWarning("MOTIS no pudo calcular el viaje: {Error}", plan?.Error);
                    return null;
                }

                var segundos = (plan.Itinerarios ?? []).Concat(plan.Directos ?? []).Select(i => i.Duracion).DefaultIfEmpty().Min();
                var tiempo = new TiempoViaje(segundos > 0 ? (int)Math.Round(segundos / 60.0) : null, Estimado: false);

                _cache.Set(clave, tiempo, VigenciaViaje);
                return tiempo;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _noConsultarHasta = _reloj.GetUtcNow() + PausaSiFalla;
                _logger.LogWarning("MOTIS no responde ({Mensaje}): los viajes se estiman por distancia por un minuto.", ex.Message);
                return null;
            }
        }

        // Transporte publico con caminatas; los demas modos, solo el viaje directo
        private static string Modos(ModoViaje modo) => modo switch
        {
            ModoViaje.Auto => "transitModes=&directModes=CAR",
            ModoViaje.Bici => $"transitModes=&directModes=BIKE&maxDirectTime={MaximoDirectoSegundos}",
            ModoViaje.APie => $"transitModes=&directModes=WALK&maxDirectTime={MaximoDirectoSegundos}",
            _ => "transitModes=TRANSIT&directModes=WALK",
        };

        private sealed class Plan
        {
            [JsonPropertyName("itineraries")]
            public List<Itinerario>? Itinerarios { get; set; }

            [JsonPropertyName("direct")]
            public List<Itinerario>? Directos { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }
        }

        private sealed class Itinerario
        {
            // Segundos
            [JsonPropertyName("duration")]
            public int Duracion { get; set; }
        }
    }
}
