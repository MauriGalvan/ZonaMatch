using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZonaMatch.Application.DTOs.Geolocation;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Geo
{
    // Forward geocoding through the local Photon instance (search-as-you-type), bounded to the imported AMBA extract.
    public sealed partial class PhotonPlaceSearchClient(HttpClient httpClient, PhotonSearchOptions options) : IPlaceSearchClient
    {
        // Photon order: minLon,minLat,maxLon,maxLat (the same area as the Nominatim viewbox)
        private const string BoundingBox = "-58.99,-34.93,-58.01,-34.33";

        // A whole neighbourhood or town, not a place: the front warns that zones are found by the analysis
        private static readonly HashSet<string> AreaTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "district", "locality", "city", "county", "state",
        };

        // Properties that hold the street and place names of an address result, to check it against the search
        private static readonly string[] AddressNameProperties = ["name", "street", "city", "locality", "district", "county"];

        // Photon tolerates typos and partial words, so there is nothing to gain from retrying variants
        public bool UsesQueryVariants => false;

        public async Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(
            string search, int limit, CancellationToken cancellationToken = default)
        {
            var photonLimit = Math.Min(limit * 3, 40);
            var url = $"api?bbox={BoundingBox}&limit={photonLimit}&q={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrWhiteSpace(options.Language))
                url += $"&lang={Uri.EscapeDataString(options.Language)}";

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // Photon is down or not started: a typed 503 instead of a generic 500
                throw new ServiceUnavailableException("El servicio de búsqueda de lugares no está disponible.", ex);
            }

            using var _ = response;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var features = GetProperty(document.RootElement, "features");
            if (features.ValueKind != JsonValueKind.Array)
                return [];

            var seenStreetLocalities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addressWords = AddressSearchWords(search);

            return features
                .EnumerateArray()
                .Where(feature => addressWords is null || MatchesAddressSearch(feature, addressWords))
                .Select(MapFeature)
                .Where(result => result is not null)
                .Cast<PlaceSearchResult>()
                .Where(result => result.Kind != "Calle"
                                 || string.IsNullOrWhiteSpace(result.Locality)
                                 || seenStreetLocalities.Add($"{result.Name.Trim()}|{result.Locality.Trim()}"))
                .Take(limit)
                .ToList();
        }

        // Photon does not require every word to match (that is what makes it typo tolerant), so a search such as
        // "avenida general paz 1200" also returns "1200 Avenida 9 de Julio". When the search has a house number, an
        // address (a result with a house number) is kept only if at least half of the searched words appear in its
        // street or place names. Results without a house number (streets, places, zones) are never filtered.
        private static IReadOnlyList<string>? AddressSearchWords(string search)
        {
            var tokens = Tokenize(search);
            if (!tokens.Any(IsNumber))
                return null;

            var words = tokens.Where(token => !IsNumber(token)).ToList();
            return words.Count > 0 ? words : null;
        }

        private static bool MatchesAddressSearch(JsonElement feature, IReadOnlyList<string> searchWords)
        {
            var properties = GetProperty(feature, "properties");
            if (ReadText(properties, "housenumber") is null)
                return true;

            var names = AddressNameProperties
                .Select(property => ReadText(properties, property))
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var resultWords = Tokenize(string.Join(' ', names));

            // StartsWith: the last word may still be half typed
            var matched = searchWords.Count(word => resultWords.Any(resultWord => resultWord.StartsWith(word, StringComparison.Ordinal)));
            return matched * 2 >= searchWords.Count;
        }

        private static bool IsNumber(string token) => token.All(char.IsDigit);

        // Lowercase words without accents: "Maipú 4493" -> maipu, 4493
        private static List<string> Tokenize(string text)
        {
            var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var plain = new string(decomposed
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .ToArray());

            return TokenPattern().Matches(plain).Select(match => match.Value).ToList();
        }

        [GeneratedRegex(@"[\p{L}\p{N}]+")]
        private static partial Regex TokenPattern();

        private static PlaceSearchResult? MapFeature(JsonElement feature)
        {
            // GeoJSON: coordinates are [longitude, latitude]
            var coordinates = GetProperty(GetProperty(feature, "geometry"), "coordinates");
            if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() < 2)
                return null;
            var longitude = ReadNumber(coordinates[0]);
            var latitude = ReadNumber(coordinates[1]);
            if (latitude is null || longitude is null)
                return null;

            var properties = GetProperty(feature, "properties");
            var houseNumber = ReadText(properties, "housenumber");
            var street = ReadText(properties, "street");
            var name = ReadText(properties, "name");
            var addressLabel = string.Join(' ', new[] { houseNumber, street }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var title = !string.IsNullOrWhiteSpace(name) ? name : addressLabel;
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var locality = ReadText(properties, "city")
                           ?? ReadText(properties, "locality")
                           ?? ReadText(properties, "district")
                           ?? ReadText(properties, "county");
            var type = ReadText(properties, "type");
            var kind = houseNumber is not null
                ? "Dirección"
                : string.Equals(type, "street", StringComparison.OrdinalIgnoreCase) ? "Calle"
                : type is not null && AreaTypes.Contains(type) ? "Zona" : "Lugar";

            return new PlaceSearchResult
            {
                Id = $"photon:{ReadText(properties, "osm_type")}{ReadText(properties, "osm_id")}",
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

        private static double? ReadNumber(JsonElement value) =>
            value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
    }
}
