using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Map;

namespace ZonaMatch.Api.Controllers;

// GeoJSON FeatureCollections used to draw map layers.
[ApiController]
[Route("api/v1/map")]
public sealed class MapController : ControllerBase
{
    private const string GeoJson = "application/geo+json";

    // GET /api/v1/map/districts?tolerance=0.0005
    [HttpGet("districts")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetDistrictsAsync(
        [FromQuery] double? tolerance,
        [FromServices] GetMapDistricts getMapDistricts)
    {
        var geoJson = await getMapDistricts.ExecuteAsync(tolerance);
        return Content(geoJson, GeoJson);
    }

    // GET /api/v1/map/communes?tolerance=0.0005
    [HttpGet("communes")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetCommunesAsync(
        [FromQuery] double? tolerance,
        [FromServices] GetMapCommunes getMapCommunes)
    {
        var geoJson = await getMapCommunes.ExecuteAsync(tolerance);
        return Content(geoJson, GeoJson);
    }

    // GET /api/v1/map/census-tracts?minLon=-58.5&minLat=-34.7&maxLon=-58.4&maxLat=-34.6
    [HttpGet("census-tracts")]
    public async Task<IActionResult> GetCensusTractsAsync(
        [FromQuery] double minLon,
        [FromQuery] double minLat,
        [FromQuery] double maxLon,
        [FromQuery] double maxLat,
        [FromQuery] double? tolerance,
        [FromServices] GetMapCensusTracts getMapCensusTracts)
    {
        var boundingBox = new BoundingBox(minLon, minLat, maxLon, maxLat);
        var geoJson = await getMapCensusTracts.ExecuteAsync(boundingBox, tolerance);
        return Content(geoJson, GeoJson);
    }

    // GET /api/v1/map/schools?minLon=&minLat=&maxLon=&maxLat=&level=&sector=
    [HttpGet("schools")]
    public async Task<IActionResult> GetSchoolsAsync(
        [FromQuery] double minLon,
        [FromQuery] double minLat,
        [FromQuery] double maxLon,
        [FromQuery] double maxLat,
        [FromServices] GetMapSchools getMapSchools,
        [FromQuery] string? level = null,
        [FromQuery] string? sector = null)
    {
        var boundingBox = new BoundingBox(minLon, minLat, maxLon, maxLat);
        var geoJson = await getMapSchools.ExecuteAsync(boundingBox, level, sector);
        return Content(geoJson, GeoJson);
    }
}
