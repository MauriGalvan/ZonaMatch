using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Infrastructure.Repositories
{
    // The zone profiles as one HybridCache entry. Immutable and sealed, so the cache hands out the same instance
    // instead of a copy on every read. HybridCache still serializes it once to measure its size, so the converter
    // writes the MarketByRooms keys (a type and a number of rooms) as "type:rooms".
    [ImmutableObject(true)]
    [JsonConverter(typeof(ZoneProfileSetJsonConverter))]
    internal sealed record ZoneProfileSet(IReadOnlyList<ZoneProfile> Profiles);

    internal sealed class ZoneProfileSetJsonConverter : JsonConverter<ZoneProfileSet>
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new HousingSizeKeyConverter() } };

        public override ZoneProfileSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<List<ZoneProfile>>(ref reader, Options) ?? []);

        public override void Write(Utf8JsonWriter writer, ZoneProfileSet value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Profiles, Options);
    }

    internal sealed class HousingSizeKeyConverter : JsonConverter<(string Type, int Rooms)>
    {
        public override (string Type, int Rooms) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Parse(reader.GetString());

        public override void Write(Utf8JsonWriter writer, (string Type, int Rooms) value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Format(value));

        public override (string Type, int Rooms) ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Parse(reader.GetString());

        public override void WriteAsPropertyName(Utf8JsonWriter writer, (string Type, int Rooms) value, JsonSerializerOptions options) =>
            writer.WritePropertyName(Format(value));

        private static string Format((string Type, int Rooms) key) =>
            $"{key.Type}:{key.Rooms.ToString(CultureInfo.InvariantCulture)}";

        private static (string Type, int Rooms) Parse(string? text)
        {
            var separator = text?.LastIndexOf(':') ?? -1;
            if (separator < 0 || !int.TryParse(text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var rooms))
                throw new JsonException($"Clave de ambientes inválida: '{text}'.");

            return (text![..separator], rooms);
        }
    }
}
