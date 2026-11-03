using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZonaMatch.Api.IntegrationTests.Support;

internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
    };
}
