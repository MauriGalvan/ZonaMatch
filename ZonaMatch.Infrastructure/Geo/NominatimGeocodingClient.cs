using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.Geolocation;

namespace ZonaMatch.Infrastructure.Geo
{
    // OpenStreetMap Nominatim reverse geocoding. Usage policy (https://operations.osmfoundation.org/policies/nominatim/):
    // identifying User-Agent (set where the HttpClient is registered), max 1 request/second, cache results.
    public class NominatimGeocodingClient : IGeocodingClient
    {
        private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(1);
        private static readonly HybridCacheEntryOptions CacheDuration = new()
        {
            Expiration = TimeSpan.FromHours(24),
            LocalCacheExpiration = TimeSpan.FromHours(24),
        };

        // Shared by every instance (the typed HttpClient is transient) to honor the 1 req/s limit globally
        private static readonly SemaphoreSlim RequestLock = new(1, 1);
        private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

        private readonly HttpClient _http;
        private readonly HybridCache _cache;

        public NominatimGeocodingClient(HttpClient http, HybridCache cache)
        {
            _http = http;
            _cache = cache;
        }

        public async Task<GeocodedAddress?> ReverseAsync(
            double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            // ~11 m precision: re-clicking the same spot is served from cache
            var cacheKey = string.Create(CultureInfo.InvariantCulture, $"nominatim:{latitude:F4}:{longitude:F4}");

            return await _cache.GetOrCreateAsync(cacheKey, token => new ValueTask<GeocodedAddress?>(QueryAsync(latitude, longitude, token)),
                CacheDuration, cancellationToken: cancellationToken);
        }

        private async Task<GeocodedAddress?> QueryAsync(double latitude, double longitude, CancellationToken cancellationToken)
        {
            var url = string.Create(CultureInfo.InvariantCulture,
                $"reverse?format=jsonv2&addressdetails=1&namedetails=1&zoom=18&accept-language=es&lat={latitude}&lon={longitude}");

            await RequestLock.WaitAsync(cancellationToken);
            try
            {
                var waitDuration = _lastRequestAt + MinimumRequestInterval - DateTimeOffset.UtcNow;
                if (waitDuration > TimeSpan.Zero)
                    await Task.Delay(waitDuration, cancellationToken);

                try
                {
                    using var response = await _http.GetAsync(url, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    return MapAddress(document.RootElement);
                }
                finally
                {
                    _lastRequestAt = DateTimeOffset.UtcNow;
                }
            }
            finally
            {
                RequestLock.Release();
            }
        }

        private static GeocodedAddress? MapAddress(JsonElement root)
        {
            // Nominatim answers 200 with {"error": "Unable to geocode"} when there is nothing near the point
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("error", out _))
                return null;

            root.TryGetProperty("address", out var address);
            root.TryGetProperty("namedetails", out var nameDetails);

            var street = GetStringProperty(address, "road") ?? GetStringProperty(address, "pedestrian") ?? GetStringProperty(address, "footway");

            return new GeocodedAddress(
                Name: GetStringProperty(root, "name") ?? GetStringProperty(nameDetails, "name"),
                Street: street,
                StreetNumber: GetStringProperty(address, "house_number"),
                Locality: GetStringProperty(address, "suburb") ?? GetStringProperty(address, "city_district") ?? GetStringProperty(address, "town")
                           ?? GetStringProperty(address, "village") ?? GetStringProperty(address, "city") ?? GetStringProperty(address, "neighbourhood"),
                Province: GetStringProperty(address, "state"),
                PostalCode: GetStringProperty(address, "postcode"),
                FullAddress: GetStringProperty(root, "display_name"));
        }

        private static string? GetStringProperty(JsonElement element, string propertyName) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var propertyValue)
            && propertyValue.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(propertyValue.GetString())
                ? propertyValue.GetString()
                : null;
    }
}
