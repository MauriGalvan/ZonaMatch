using System.Globalization;
using System.Text.Json;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.Geolocation;

namespace ZonaMatch.Infrastructure.Geo
{
    // Reverse geocoding through the local Photon instance (the same index as the place search).
    // Photon answers with the nearest object of any kind: an address (house numbers interpolated by Nominatim
    // included), a place or a street. The district, commune and province are added later from the local boundaries.
    public sealed class PhotonGeocodingClient(HttpClient httpClient, PhotonSearchOptions options) : IGeocodingClient
    {
        // Farther than this the nearest object says nothing about the picked spot
        private const double MaxDistanceMeters = 100;

        // Results that are a whole area: their name is a locality, not a place or a street
        private static readonly HashSet<string> AreaTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "district", "locality", "city", "county", "state", "country",
        };

        public async Task<GeocodedAddress?> ReverseAsync(
            double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            var url = string.Create(CultureInfo.InvariantCulture, $"reverse?lat={latitude}&lon={longitude}&limit=1");
            if (!string.IsNullOrWhiteSpace(options.Language))
                url += $"&lang={Uri.EscapeDataString(options.Language)}";

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                // ReverseGeocode turns HttpRequestException into a 502
                throw new HttpRequestException("Photon no respondió a tiempo.", exception);
            }

            using var _ = response;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var features = GetProperty(document.RootElement, "features");
            if (features.ValueKind != JsonValueKind.Array || features.GetArrayLength() == 0)
                return null;

            return MapFeature(features[0], latitude, longitude);
        }

        private static GeocodedAddress? MapFeature(JsonElement feature, double latitude, double longitude)
        {
            // GeoJSON: coordinates are [longitude, latitude]
            var coordinates = GetProperty(GetProperty(feature, "geometry"), "coordinates");
            if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() < 2
                || coordinates[0].ValueKind != JsonValueKind.Number || coordinates[1].ValueKind != JsonValueKind.Number)
                return null;

            if (DistanceMeters(latitude, longitude, coordinates[1].GetDouble(), coordinates[0].GetDouble()) > MaxDistanceMeters)
                return null;

            var properties = GetProperty(feature, "properties");
            var type = ReadText(properties, "type");
            var name = ReadText(properties, "name");
            var city = ReadText(properties, "city");
            var subLocality = ReadText(properties, "locality");

            // In the city of Buenos Aires "locality" is the barrio and "city" is the whole city; elsewhere "locality" is a
            // quarter inside the town ("city"), which is the name people use. Seen in two samples: check it on more points.
            var locality = (string.Equals(city, "Buenos Aires", StringComparison.OrdinalIgnoreCase) ? subLocality : null)
                           ?? city
                           ?? subLocality
                           ?? ReadText(properties, "district");

            string? placeName = null;
            string? street = null;
            string? houseNumber = null;

            if (string.Equals(type, "street", StringComparison.OrdinalIgnoreCase))
            {
                street = name;
            }
            else if (type is not null && AreaTypes.Contains(type))
            {
                locality = name ?? locality;
            }
            else
            {
                placeName = name;
                street = ReadText(properties, "street");
                houseNumber = ReadText(properties, "housenumber");
            }

            var streetAddress = string.Join(' ', new[] { street, houseNumber }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var fullAddress = string.Join(", ",
                new[] { placeName, streetAddress, locality }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (placeName is null && street is null && locality is null)
                return null;

            return new GeocodedAddress(
                Name: placeName,
                Street: street,
                StreetNumber: houseNumber,
                Locality: locality,
                Province: ReadText(properties, "state"),
                PostalCode: ReadText(properties, "postcode"),
                FullAddress: fullAddress.Length > 0 ? fullAddress : null);
        }

        private static double DistanceMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
        {
            const double EarthRadiusMeters = 6_371_000;
            var deltaLatitude = ToRadians(latitudeB - latitudeA);
            var deltaLongitude = ToRadians(longitudeB - longitudeA);
            var a = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2)
                    + Math.Cos(ToRadians(latitudeA)) * Math.Cos(ToRadians(latitudeB))
                    * Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
            return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;

        private static JsonElement GetProperty(JsonElement element, string propertyName) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
                ? value
                : default;

        private static string? ReadText(JsonElement element, string propertyName)
        {
            var value = GetProperty(element, propertyName);
            return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()
                : null;
        }
    }
}
