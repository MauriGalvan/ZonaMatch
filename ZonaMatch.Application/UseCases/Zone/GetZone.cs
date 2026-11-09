using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using DomainZone = ZonaMatch.Domain.Entities.Zone;

namespace ZonaMatch.Application.UseCases.Zone;

public sealed class GetZone(IZoneRepository zoneRepository)
{
    // Names keep their accents as they are ("Núñez") instead of \u escapes.
    private static readonly JsonSerializerOptions GeoJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // GeoJSON Feature with the zone boundary and the properties the map shows.
    public async Task<string> ExecuteAsync(
        string slug,
        double? tolerance,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        var zone = await zoneRepository.GetBySlugAsync(
            slug, ZoneQueryPolicy.NormalizeBoundaryTolerance(tolerance), cancellationToken)
            ?? throw new NotFoundException("No se encontró una zona con ese identificador.");

        return ToFeature(zone);
    }

    private static string ToFeature(DomainZone zone)
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

        return feature.ToJsonString(GeoJsonOptions);
    }
}
