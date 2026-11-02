using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.Geolocation.Requests;
using ZonaMatch.Application.DTOs.Geolocation.Responses;
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

    // GET /api/v1/geolocation/summary?latitud=-34.6&longitud=-58.45&radioMetros=1000
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] GetLocationSummary getLocationSummary,
        [FromQuery(Name = "radioMetros")] double radiusMeters = 1000)
    {
        var summary = await getLocationSummary.ExecuteAsync(latitude, longitude, radiusMeters);
        return Ok(summary);
    }

    // GET /api/v1/geolocation/schools?latitud=&longitud=&radioMetros=&nivel=&sector=
    [HttpGet("schools")]
    public async Task<IActionResult> GetNearbySchools(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] GetNearbySchools getNearbySchools,
        [FromQuery(Name = "radioMetros")] double radiusMeters = 1000,
        [FromQuery(Name = "nivel")] string? level = null,
        [FromQuery] string? sector = null)
    {
        var schools = await getNearbySchools.ExecuteAsync(latitude, longitude, radiusMeters, level, sector);
        return Ok(schools);
    }

    // GET /api/v1/geolocation/schools/nearest?latitud=&longitud=&cantidad=5
    [HttpGet("schools/nearest")]
    public async Task<IActionResult> GetNearestSchools(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] GetNearestSchools getNearestSchools,
        [FromQuery(Name = "cantidad")] int quantity = 5)
    {
        var schools = await getNearestSchools.ExecuteAsync(latitude, longitude, quantity);
        return Ok(schools);
    }

    // GET /api/v1/geolocation/points-of-interest?latitud=&longitud=&radioMetros=1000&categorias=transporte,salud
    // Without categorias it returns every category.
    [HttpGet("points-of-interest")]
    public async Task<IActionResult> GetNearbyPointsOfInterest(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] GetNearbyPointsOfInterest getNearbyPointsOfInterest,
        [FromQuery(Name = "radioMetros")] double radiusMeters = 1000,
        [FromQuery(Name = "categorias")] string? categories = null,
        CancellationToken cancellationToken = default)
    {
        var points = await getNearbyPointsOfInterest.ExecuteAsync(
            latitude, longitude, radiusMeters, categories, cancellationToken);
        return Ok(points);
    }

    // GET /api/v1/geolocation/points-of-interest/summary?latitud=&longitud=&radioMetros=1000
    [HttpGet("points-of-interest/summary")]
    public async Task<IActionResult> GetPointsOfInterestSummary(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] GetPointsOfInterestSummary getPointsOfInterestSummary,
        [FromQuery(Name = "radioMetros")] double radiusMeters = 1000,
        CancellationToken cancellationToken = default)
    {
        var summary = await getPointsOfInterestSummary.ExecuteAsync(
            latitude, longitude, radiusMeters, cancellationToken);
        return Ok(summary);
    }

    // GET /api/v1/geolocation/district?latitud=&longitud=
    [HttpGet("district")]
    public async Task<IActionResult> GetDistrictByLocation(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] GetDistrictByLocation getDistrictByLocation)
    {
        return Ok(await getDistrictByLocation.ExecuteAsync(latitude, longitude));
    }

    // GET /api/v1/geolocation/commune?latitud=&longitud=
    [HttpGet("commune")]
    public async Task<IActionResult> GetCommuneByLocation(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] GetCommuneByLocation getCommuneByLocation)
    {
        return Ok(await getCommuneByLocation.ExecuteAsync(latitude, longitude));
    }

    // GET /api/v1/geolocation/address?latitud=&longitud=
    // Returns the place name and address of a point picked on the map.
    [HttpGet("address")]
    public async Task<IActionResult> ReverseGeocode(
        [FromQuery(Name = "latitud")] double latitude,
        [FromQuery(Name = "longitud")] double longitude,
        [FromServices] ReverseGeocode reverseGeocode,
        CancellationToken cancellationToken)
    {
        return Ok(await reverseGeocode.ExecuteAsync(latitude, longitude, cancellationToken));
    }
}
