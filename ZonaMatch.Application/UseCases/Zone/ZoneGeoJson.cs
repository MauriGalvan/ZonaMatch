using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DomainZone = ZonaMatch.Domain.Entities.Zone;

namespace ZonaMatch.Application.UseCases.Zone;

// GeoJSON Feature that GET /api/v1/zones/{slug} returns: the zone boundary and the properties the map shows.
internal static class ZoneGeoJson
{
    // Names keep their accents as they are ("Núñez") instead of \u escapes.
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string ToFeature(DomainZone zone)
    {
        var boundary = zone.Boundary
            ?? throw new InvalidOperationException($"Zone {zone.Slug} was loaded without its boundary.");

        var jurisdictions = zone.Jurisdictions
            .Select(jurisdiction => (JsonNode)new JsonObject
            {
                ["name"] = jurisdiction.Name,
                ["administrativeLevel"] = jurisdiction.AdministrativeLevel
            })
            .ToArray();

        var feature = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = zone.FeatureId,
            ["properties"] = new JsonObject
            {
                ["slug"] = zone.Slug,
                ["name"] = zone.Name,
                ["administrativeLevel"] = zone.AdministrativeLevel,
                ["jurisdictions"] = new JsonArray(jurisdictions),
                ["areaKm2"] = zone.AreaKm2,
                ["center"] = new JsonObject
                {
                    ["latitude"] = boundary.CenterLatitude,
                    ["longitude"] = boundary.CenterLongitude
                }
            },
            ["geometry"] = JsonNode.Parse(boundary.GeometryGeoJson)
        };

        return feature.ToJsonString(Options);
    }
}
