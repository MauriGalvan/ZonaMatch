using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.PointOfInterest.Responses;
using ZonaMatch.Application.DTOs.Transit.Responses;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.DTOs.ZoneIndicator.Responses;
using ZonaMatch.Application.UseCases.Zone;

namespace ZonaMatch.Api.Controllers;

// Zones (neighborhoods, communes, localities, districts) from OpenStreetMap administrative boundaries.
[ApiController]
[Route("api/v1/zones")]
public sealed class ZonesController : ControllerBase
{
    private const string GeoJson = "application/geo+json";

    // GET /api/v1/zones?search=villa l&limit=10
    // Matching is accent-insensitive; names that start with the query are returned first.
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ZoneSearchResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ZoneSearchResultResponse>>> SearchZones(
        [FromQuery] string? search,
        [FromServices] SearchZones searchZones,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var zones = await searchZones.ExecuteAsync(search, limit, cancellationToken);
        return Ok(zones);
    }

    // GET /api/v1/zones/villa-luro?tolerance=0.00015
    // Returns a GeoJSON Feature with the zone geometry and its administrative properties.
    [HttpGet("{slug}")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetZoneAsync(
        [FromRoute] string slug,
        [FromQuery] double? tolerance,
        [FromServices] GetZone getZone,
        CancellationToken cancellationToken)
    {
        var zone = await getZone.ExecuteAsync(slug, tolerance, cancellationToken);
        return Content(zone, GeoJson);
    }

    // GET /api/v1/zones/villa-luro/points-of-interest?categories=category-codes
    // Unknown zones return an empty list, matching the existing repository behavior.
    [HttpGet("{slug}/points-of-interest")]
    [ProducesResponseType(typeof(NearbyPointsOfInterestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NearbyPointsOfInterestResponse>> GetZonePointsOfInterest(
        [FromRoute] string slug,
        [FromQuery] string? categories,
        [FromServices] GetZonePointsOfInterest getZonePointsOfInterest,
        CancellationToken cancellationToken)
    {
        var points = await getZonePointsOfInterest.ExecuteAsync(slug, categories, cancellationToken);
        return Ok(points);
    }

    // GET /api/v1/zones/villa-luro/points-of-interest/summary
    [HttpGet("{slug}/points-of-interest/summary")]
    [ProducesResponseType(typeof(PointsOfInterestSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PointsOfInterestSummaryResponse>> GetZonePointsOfInterestSummary(
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
    [ProducesResponseType<ZoneIndicatorsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZoneIndicatorsAsync(
        [FromRoute] string slug,
        [FromServices] GetZoneIndicators getZoneIndicators,
        CancellationToken cancellationToken)
    {
        return Ok(await getZoneIndicators.ExecuteAsync(slug, cancellationToken));
    }

    // GET /api/v1/zones/villa-luro/transit
    [HttpGet("{slug}/transit")]
    [ResponseCache(Duration = 3600)]
    [ProducesResponseType<ZoneTransitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZoneTransitAsync(
        [FromRoute] string slug,
        [FromServices] GetZoneTransit getZoneTransit,
        CancellationToken cancellationToken)
    {
        return Ok(await getZoneTransit.ExecuteAsync(slug, cancellationToken));
    }

    // GET /api/v1/zones/villa-luro/transit/routes
    // Paths of the bus, train, and subway lines inside the zone, for the map layer.
    [HttpGet("{slug}/transit/routes")]
    [ResponseCache(Duration = 3600)]
    [ProducesResponseType<ZoneTransitRouteListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZoneTransitRoutesAsync(
        [FromRoute] string slug,
        [FromServices] GetZoneTransitRoutes getZoneTransitRoutes,
        CancellationToken cancellationToken)
    {
        return Ok(await getZoneTransitRoutes.ExecuteAsync(slug, cancellationToken));
    }
}
