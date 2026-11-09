using System.Text.Json;
using ZonaMatch.Application.DTOs.Transit.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zone;

// Paths of the transit lines inside a zone, for the map layer.
public sealed class GetZoneTransitRoutes(
    IZoneTransitRepository transitRepository,
    IZoneRepository zoneRepository)
{
    // Same display order as the transit summary: rail first, then buses.
    private static readonly string[] ModeOrder = ["subte", "tren", "colectivo"];

    public async Task<ZoneTransitRouteListResponse> ExecuteAsync(string slug, CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        var routes = await transitRepository.ListRoutesAsync(slug, cancellationToken);

        // No lines may mean the zone has no transit service or does not exist.
        if (routes.Count == 0 && !await zoneRepository.ExistsAsync(slug, cancellationToken))
            throw new NotFoundException("No se encontró una zona con ese identificador.");

        var response = routes
            .OrderBy(route => Array.IndexOf(ModeOrder, route.Mode) is var order and >= 0 ? order : ModeOrder.Length)
            .ThenBy(route => int.TryParse(route.Line, out var number) ? number : int.MaxValue)
            .ThenBy(route => route.Line, StringComparer.Ordinal)
            .Select(route => new TransitRouteResponse(route.Mode, route.Line, ToPaths(route)))
            .Where(route => route.Paths.Count > 0)
            .ToList();

        return new ZoneTransitRouteListResponse(slug, response);
    }

    // GeoJSON LineString or MultiLineString ([longitude, latitude]) to paths of [latitude, longitude] points.
    private static IReadOnlyList<IReadOnlyList<double[]>> ToPaths(TransitRouteGeometry route)
    {
        using var document = JsonDocument.Parse(route.GeometryGeoJson);
        var geometry = document.RootElement;
        var coordinates = geometry.GetProperty("coordinates");

        return geometry.GetProperty("type").GetString() switch
        {
            "LineString" => [ToPath(coordinates)],
            "MultiLineString" => coordinates.EnumerateArray().Select(ToPath).ToList(),
            _ => [],
        };
    }

    private static IReadOnlyList<double[]> ToPath(JsonElement line) =>
        line.EnumerateArray()
            .Select(point => new[] { point[1].GetDouble(), point[0].GetDouble() })
            .ToList();
}
