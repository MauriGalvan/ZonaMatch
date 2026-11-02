using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.Geolocation.Requests;
using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.DTOs.PointsOfInterest.Responses;
using ZonaMatch.Application.UseCases.Geolocation;

namespace ZonaMatch.Api.Controllers;

[ApiController]
[Route("api/v1/geolocation")]
public sealed class GeolocationController : ControllerBase
{
    [HttpGet("places")]
    [ProducesResponseType(typeof(IReadOnlyList<PlaceSearchResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlaceSearchResultResponse>>> SearchPlaces(
        [FromQuery] SearchPlacesRequest request,
        [FromServices] SearchPlaces searchPlaces,
        CancellationToken cancellationToken)
    {
        var results = await searchPlaces.ExecuteAsync(request.Search, cancellationToken);
        return Ok(results);
    }

    // GET /api/v1/geolocation/summary?latitude=-34.6&longitude=-58.45&radiusMeters=1000
    [HttpGet("summary")]
    [ProducesResponseType(typeof(LocationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocationSummaryResponse>> GetSummary(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] GetLocationSummary getLocationSummary,
        [FromQuery] double radiusMeters = 1000)
    {
        var summary = await getLocationSummary.ExecuteAsync(latitude, longitude, radiusMeters);
        return Ok(summary);
    }

    // GET /api/v1/geolocation/schools?latitude=&longitude=&radiusMeters=&level=&sector=
    [HttpGet("schools")]
    [ProducesResponseType(typeof(IReadOnlyList<SchoolResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SchoolResponse>>> GetNearbySchools(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] GetNearbySchools getNearbySchools,
        [FromQuery] double radiusMeters = 1000,
        [FromQuery] string? level = null,
        [FromQuery] string? sector = null)
    {
        var schools = await getNearbySchools.ExecuteAsync(latitude, longitude, radiusMeters, level, sector);
        return Ok(schools);
    }

    // GET /api/v1/geolocation/schools/nearest?latitude=&longitude=&quantity=5
    [HttpGet("schools/nearest")]
    [ProducesResponseType(typeof(IReadOnlyList<SchoolResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SchoolResponse>>> GetNearestSchools(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] GetNearestSchools getNearestSchools,
        [FromQuery] int quantity = 5)
    {
        var schools = await getNearestSchools.ExecuteAsync(latitude, longitude, quantity);
        return Ok(schools);
    }

    // GET /api/v1/geolocation/points-of-interest?latitude=&longitude=&radiusMeters=1000&categories=category-codes
    // Without categories it returns every category.
    [HttpGet("points-of-interest")]
    [ProducesResponseType(typeof(NearbyPointsOfInterestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NearbyPointsOfInterestResponse>> GetNearbyPointsOfInterest(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] GetNearbyPointsOfInterest getNearbyPointsOfInterest,
        [FromQuery] double radiusMeters = 1000,
        [FromQuery] string? categories = null,
        CancellationToken cancellationToken = default)
    {
        var points = await getNearbyPointsOfInterest.ExecuteAsync(
            latitude, longitude, radiusMeters, categories, cancellationToken);
        return Ok(points);
    }

    // GET /api/v1/geolocation/points-of-interest/summary?latitude=&longitude=&radiusMeters=1000
    [HttpGet("points-of-interest/summary")]
    [ProducesResponseType(typeof(PointsOfInterestSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PointsOfInterestSummaryResponse>> GetPointsOfInterestSummary(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] GetPointsOfInterestSummary getPointsOfInterestSummary,
        [FromQuery] double radiusMeters = 1000,
        CancellationToken cancellationToken = default)
    {
        var summary = await getPointsOfInterestSummary.ExecuteAsync(
            latitude, longitude, radiusMeters, cancellationToken);
        return Ok(summary);
    }

    // GET /api/v1/geolocation/district?latitude=&longitude=
    [HttpGet("district")]
    [ProducesResponseType(typeof(DistrictResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DistrictResponse>> GetDistrictByLocation(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] GetDistrictByLocation getDistrictByLocation)
    {
        return Ok(await getDistrictByLocation.ExecuteAsync(latitude, longitude));
    }

    // GET /api/v1/geolocation/commune?latitude=&longitude=
    [HttpGet("commune")]
    [ProducesResponseType(typeof(CommuneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommuneResponse>> GetCommuneByLocation(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] GetCommuneByLocation getCommuneByLocation)
    {
        return Ok(await getCommuneByLocation.ExecuteAsync(latitude, longitude));
    }

    // GET /api/v1/geolocation/address?latitude=&longitude=
    // Returns the place name and address of a point picked on the map.
    [HttpGet("address")]
    [ProducesResponseType(typeof(ReverseGeocodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReverseGeocodeResponse>> ReverseGeocode(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] ReverseGeocode reverseGeocode,
        CancellationToken cancellationToken)
    {
        return Ok(await reverseGeocode.ExecuteAsync(latitude, longitude, cancellationToken));
    }
}
