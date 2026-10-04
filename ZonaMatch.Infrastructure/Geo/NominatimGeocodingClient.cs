using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Geo
{
    // OpenStreetMap Nominatim reverse geocoding. Usage policy (https://operations.osmfoundation.org/policies/nominatim/):
    // identifying User-Agent (set where the HttpClient is registered), max 1 request/second, cache results.
    public class NominatimGeocodingClient : IGeocodingClient
    {
        private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan CacheDuracion = TimeSpan.FromHours(24);

        // Shared by every instance (the typed HttpClient is transient) to honor the 1 req/s limit globally
        private static readonly SemaphoreSlim Turno = new(1, 1);
        private static DateTimeOffset _ultimaLlamada = DateTimeOffset.MinValue;

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;

        public NominatimGeocodingClient(HttpClient http, IMemoryCache cache)
        {
            _http = http;
            _cache = cache;
        }

        public async Task<DireccionGeocodificadaDto?> ReversaAsync(
            double latitud, double longitud, CancellationToken cancellationToken = default)
        {
            // ~11 m precision: re-clicking the same spot is served from cache
            var cacheKey = string.Create(CultureInfo.InvariantCulture, $"nominatim:{latitud:F4}:{longitud:F4}");

            if (_cache.TryGetValue(cacheKey, out DireccionGeocodificadaDto? cached))
                return cached;

            var resultado = await ConsultarAsync(latitud, longitud, cancellationToken);
            _cache.Set(cacheKey, resultado, CacheDuracion);
            return resultado;
        }

        private async Task<DireccionGeocodificadaDto?> ConsultarAsync(double latitud, double longitud, CancellationToken ct)
        {
            var url = string.Create(CultureInfo.InvariantCulture,
                $"reverse?format=jsonv2&addressdetails=1&namedetails=1&zoom=18&accept-language=es&lat={latitud}&lon={longitud}");

            await Turno.WaitAsync(ct);
            try
            {
                var espera = _ultimaLlamada + IntervaloMinimo - DateTimeOffset.UtcNow;
                if (espera > TimeSpan.Zero)
                    await Task.Delay(espera, ct);

                try
                {
                    using var response = await _http.GetAsync(url, ct);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    return Mapear(doc.RootElement);
                }
                finally
                {
                    _ultimaLlamada = DateTimeOffset.UtcNow;
                }
            }
            finally
            {
                Turno.Release();
            }
        }

        private static DireccionGeocodificadaDto? Mapear(JsonElement raiz)
        {
            // Nominatim answers 200 with {"error": "Unable to geocode"} when there is nothing near the point
            if (raiz.ValueKind != JsonValueKind.Object || raiz.TryGetProperty("error", out _))
                return null;

            raiz.TryGetProperty("address", out var address);
            raiz.TryGetProperty("namedetails", out var nombres);

            var calle = Texto(address, "road") ?? Texto(address, "pedestrian") ?? Texto(address, "footway");

            return new DireccionGeocodificadaDto(
                Nombre: Texto(raiz, "name") ?? Texto(nombres, "name"),
                Calle: calle,
                Altura: Texto(address, "house_number"),
                Localidad: Texto(address, "suburb") ?? Texto(address, "city_district") ?? Texto(address, "town")
                           ?? Texto(address, "village") ?? Texto(address, "city") ?? Texto(address, "neighbourhood"),
                Provincia: Texto(address, "state"),
                CodigoPostal: Texto(address, "postcode"),
                DireccionCompleta: Texto(raiz, "display_name"));
        }

        private static string? Texto(JsonElement elemento, string propiedad) =>
            elemento.ValueKind == JsonValueKind.Object
            && elemento.TryGetProperty(propiedad, out var valor)
            && valor.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(valor.GetString())
                ? valor.GetString()
                : null;
    }
}
