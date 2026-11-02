using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Zones;

namespace ZonaMatch.Api.Controllers;

// Zones (neighborhoods, communes, localities, districts) from OpenStreetMap administrative boundaries.
[ApiController]
[Route("api/v1/zones")]
public sealed class ZonesController : ControllerBase
{
    private const string GeoJson = "application/geo+json";

    // GET /api/v1/zones?buscar=villa l&limite=10
    // Matching is accent-insensitive; names that start with the query are returned first.
    [HttpGet]
    public async Task<IActionResult> SearchZones(
        [FromQuery(Name = "buscar")] string? search,
        [FromServices] SearchZones searchZones,
        [FromQuery(Name = "limite")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var zones = await searchZones.ExecuteAsync(search, limit, cancellationToken);
        return Ok(zones);
    }

    // GET /api/v1/zones/villa-luro?tolerancia=0.00015
    // Returns a GeoJSON Feature with the zone geometry and its administrative properties.
    [HttpGet("{slug}")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetZone(
        [FromRoute] string slug,
        [FromQuery(Name = "tolerancia")] double? tolerance,
        [FromServices] GetZone getZone,
        CancellationToken cancellationToken)
    {
        var zone = await getZone.ExecuteAsync(slug, tolerance, cancellationToken);
        return Content(zone, GeoJson);
    }

    // GET /api/v1/zones/villa-luro/points-of-interest?categorias=transporte,salud
    // Unknown zones return an empty list, matching the existing repository behavior.
    [HttpGet("{slug}/points-of-interest")]
    public async Task<IActionResult> GetZonePointsOfInterest(
        [FromRoute] string slug,
        [FromQuery(Name = "categorias")] string? categories,
        [FromServices] GetZonePointsOfInterest getZonePointsOfInterest,
        CancellationToken cancellationToken)
    {
        var points = await getZonePointsOfInterest.ExecuteAsync(slug, categories, cancellationToken);
        return Ok(points);
    }

    // GET /api/v1/zones/villa-luro/points-of-interest/summary
    [HttpGet("{slug}/points-of-interest/summary")]
    public async Task<IActionResult> GetZonePointsOfInterestSummary(
        [FromRoute] string slug,
        [FromServices] GetZonePointsOfInterestSummary getZonePointsOfInterestSummary,
        CancellationToken cancellationToken)
    {
        var summary = await getZonePointsOfInterestSummary.ExecuteAsync(slug, cancellationToken);
        return Ok(summary);
    }

    // GET /api/v1/zones/villa-luro/indicators
    [HttpGet("{slug}/indicators")]
    [ResponseCache(Duration = 3600)]
    [ProducesResponseType<IndicadoresZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZoneIndicators(
        [FromRoute] string slug,
        [FromServices] GetZoneIndicators getZoneIndicators,
        CancellationToken cancellationToken)
    {
        return Ok(await getZoneIndicators.ExecuteAsync(slug, cancellationToken));
    }

    // GET /api/v1/zones/villa-luro/transit
    [HttpGet("{slug}/transit")]
    [ResponseCache(Duration = 3600)]
    [ProducesResponseType<TransporteZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZoneTransit(
        [FromRoute] string slug,
        [FromServices] GetZoneTransit getZoneTransit,
        CancellationToken cancellationToken)
    {
        return Ok(await getZoneTransit.ExecuteAsync(slug, cancellationToken));
    }
}
