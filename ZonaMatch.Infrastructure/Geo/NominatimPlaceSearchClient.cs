using System.Globalization;
using System.Text.Json;
using ZonaMatch.Application.DTOs.Geolocation;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Geo
{
    // Forward geocoding through the local Nominatim instance, bounded to the imported AMBA extract.
    public sealed class NominatimPlaceSearchClient(HttpClient httpClient) : IPlaceSearchClient
    {
        private const string Viewbox = "-58.99,-34.33,-58.01,-34.93";

        public async Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(
            string search, int limit, CancellationToken cancellationToken = default)
        {
            var nominatimLimit = Math.Min(limit * 3, 40);
            var url = $"search?format=jsonv2&addressdetails=1&namedetails=1&accept-language=es" +
                      $"&countrycodes=ar&viewbox={Viewbox}&bounded=1&limit={nominatimLimit}&q={Uri.EscapeDataString(search)}";

            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var seenStreetLocalities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return document.RootElement
                .EnumerateArray()
                .Select(MapResult)
                .Where(result => result is not null)
                .Cast<PlaceSearchResult>()
                .Where(result => result.Kind != "Calle"
                                 || string.IsNullOrWhiteSpace(result.Locality)
                                 || seenStreetLocalities.Add($"{result.Name.Trim()}|{result.Locality.Trim()}"))
                .Take(limit)
                .ToList();
        }

        private static PlaceSearchResult? MapResult(JsonElement result)
        {
            var latitude = ParseCoordinate(ReadText(result, "lat"));
            var longitude = ParseCoordinate(ReadText(result, "lon"));
            if (latitude is null || longitude is null)
                return null;

            var address = GetProperty(result, "address");
            var houseNumber = ReadText(address, "house_number");
            var street = ReadText(address, "road")
                         ?? ReadText(address, "pedestrian")
                         ?? ReadText(address, "footway");
            var name = ReadText(result, "name");
            var addressLabel = string.Join(' ', new[] { houseNumber, street }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var title = !string.IsNullOrWhiteSpace(name)
                ? name
                : addressLabel.Length > 0 ? addressLabel : ReadText(result, "display_name");
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var locality = ReadText(address, "town")
                           ?? ReadText(address, "village")
                           ?? ReadText(address, "city")
                           ?? ReadText(address, "suburb")
                           ?? ReadText(address, "quarter")
                           ?? ReadText(address, "neighbourhood")
                           ?? ReadText(address, "municipality");
            var addressType = ReadText(result, "addresstype");
            var kind = houseNumber is not null
                ? "Dirección"
                : string.Equals(addressType, "road", StringComparison.OrdinalIgnoreCase) ? "Calle" : "Lugar";

            return new PlaceSearchResult
            {
                Id = ReadText(result, "place_id") is { } placeId
                    ? $"nominatim:{placeId}"
                    : $"{ReadText(result, "osm_type")}:{ReadText(result, "osm_id")}",
                Name = title,
                Kind = kind,
                Locality = locality,
                Latitude = latitude.Value,
                Longitude = longitude.Value,
                IsApproximate = false
            };
        }

        private static JsonElement GetProperty(JsonElement element, string propertyName) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
                ? value
                : default;

        private static string? ReadText(JsonElement element, string propertyName)
        {
            var value = GetProperty(element, propertyName);
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        private static double? ParseCoordinate(string? value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate)
                ? coordinate
                : null;
    }
}
