using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.Geolocation.Requests;
using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("api/v1/geolocation")]
    public class GeolocationController : ControllerBase
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
        public async Task<IActionResult> GetResumen(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService,
            [FromQuery] double radioMetros = 1000)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            var resumen = await geolocationService.GetResumenUbicacionAsync(latitud, longitud, radioMetros);
            return Ok(resumen);
        }

        // GET /api/v1/geolocation/schools?latitud=&longitud=&radioMetros=&nivel=&sector=
        [HttpGet("schools")]
        public async Task<IActionResult> GetEscuelasCercanas(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService,
            [FromQuery] double radioMetros = 1000,
            [FromQuery] string? nivel = null, [FromQuery] string? sector = null)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            var escuelas = await geolocationService.GetEscuelasCercanasAsync(latitud, longitud, radioMetros, nivel, sector);
            return Ok(escuelas);
        }

        // GET /api/v1/geolocation/schools/nearest?latitud=&longitud=&cantidad=5
        [HttpGet("schools/nearest")]
        public async Task<IActionResult> GetEscuelasMasCercanas(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService,
            [FromQuery] int cantidad = 5)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            if (cantidad <= 0)
                return BadRequest("La cantidad debe ser mayor a 0.");

            var escuelas = await geolocationService.GetEscuelasMasCercanasAsync(latitud, longitud, cantidad);
            return Ok(escuelas);
        }

        // GET /api/v1/geolocation/points-of-interest?latitud=&longitud=&radioMetros=1000&categorias=transporte,salud
        // Without categorias it returns every category (see CategoriaPuntoInteres)
        [HttpGet("points-of-interest")]
        public async Task<IActionResult> GetPuntosInteresCercanos(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService,
            [FromQuery] double radioMetros = 1000,
            [FromQuery] string? categorias = null, CancellationToken cancellationToken = default)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            if (radioMetros > GeolocationService.MaxRadioPuntosInteresMetros)
                return BadRequest($"El radio no puede superar los {GeolocationService.MaxRadioPuntosInteresMetros} metros.");

            if (!CategoriaPuntoInteres.TryParseLista(categorias, out var codigos, out error))
                return BadRequest(error);

            var puntos = await geolocationService.GetPuntosInteresCercanosAsync(
                latitud, longitud, radioMetros, codigos, cancellationToken);
            return Ok(puntos);
        }

        // GET /api/v1/geolocation/points-of-interest/summary?latitud=&longitud=&radioMetros=1000
        [HttpGet("points-of-interest/summary")]
        public async Task<IActionResult> GetResumenPuntosInteres(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService,
            [FromQuery] double radioMetros = 1000,
            CancellationToken cancellationToken = default)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            if (radioMetros > GeolocationService.MaxRadioPuntosInteresMetros)
                return BadRequest($"El radio no puede superar los {GeolocationService.MaxRadioPuntosInteresMetros} metros.");

            var resumen = await geolocationService.GetResumenPuntosInteresAsync(latitud, longitud, radioMetros, cancellationToken);
            return Ok(resumen);
        }

        // GET /api/v1/geolocation/district?latitud=&longitud=
        [HttpGet("district")]
        public async Task<IActionResult> GetPartidoPorUbicacion(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            var partido = await geolocationService.GetPartidoPorUbicacionAsync(latitud, longitud);
            return partido is null ? NotFound("No se encontro un partido para esa ubicacion.") : Ok(partido);
        }

        // GET /api/v1/geolocation/commune?latitud=&longitud=
        [HttpGet("commune")]
        public async Task<IActionResult> GetComunaPorUbicacion(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            var comuna = await geolocationService.GetComunaPorUbicacionAsync(latitud, longitud);
            return comuna is null ? NotFound("No se encontro una comuna para esa ubicacion.") : Ok(comuna);
        }

        // GET /api/v1/geolocation/address?latitud=&longitud=
        // Place name + address of a point picked on the map (reverse geocoding)
        [HttpGet("address")]
        public async Task<IActionResult> GetDireccionPorUbicacion(
            [FromQuery] double latitud,
            [FromQuery] double longitud,
            [FromServices] IGeolocationService geolocationService,
            [FromServices] ILogger<GeolocationController> logger,
            CancellationToken cancellationToken)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            try
            {
                var direccion = await geolocationService.GetDireccionPorUbicacionAsync(latitud, longitud, cancellationToken);
                return direccion is null ? NotFound("No se encontro una direccion para esa ubicacion.") : Ok(direccion);
            }
            catch (HttpRequestException ex)
            {
                // Coordinates are not logged: they are the user's location
                logger.LogWarning(ex, "Reverse geocoding service is unavailable");
                return StatusCode(StatusCodes.Status502BadGateway, "El servicio de geocodificacion no esta disponible.");
            }
        }

        private static bool CoordenadasValidas(double latitud, double longitud, double radioMetros, out string error)
        {
            if (latitud is < -90 or > 90)
            {
                error = "La latitud debe estar entre -90 y 90.";
                return false;
            }

            if (longitud is < -180 or > 180)
            {
                error = "La longitud debe estar entre -180 y 180.";
                return false;
            }

            if (radioMetros < 0)
            {
                error = "El radio no puede ser negativo.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
