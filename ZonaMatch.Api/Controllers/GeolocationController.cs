using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GeolocationController : ControllerBase
    {
        private readonly IGeolocationService _geolocationService;

        public GeolocationController(IGeolocationService geolocationService)
        {
            _geolocationService = geolocationService;
        }

        // GET /Geolocation/resumen?latitud=-34.6&longitud=-58.45&radioMetros=1000
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen(
            [FromQuery] double latitud, [FromQuery] double longitud, [FromQuery] double radioMetros = 1000)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            var resumen = await _geolocationService.GetResumenUbicacionAsync(latitud, longitud, radioMetros);
            return Ok(resumen);
        }

        // GET /Geolocation/escuelas?latitud=&longitud=&radioMetros=&nivel=&sector=
        [HttpGet("escuelas")]
        public async Task<IActionResult> GetEscuelasCercanas(
            [FromQuery] double latitud, [FromQuery] double longitud, [FromQuery] double radioMetros = 1000,
            [FromQuery] string? nivel = null, [FromQuery] string? sector = null)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            var escuelas = await _geolocationService.GetEscuelasCercanasAsync(latitud, longitud, radioMetros, nivel, sector);
            return Ok(escuelas);
        }

        // GET /Geolocation/escuelas/mas-cercanas?latitud=&longitud=&cantidad=5
        [HttpGet("escuelas/mas-cercanas")]
        public async Task<IActionResult> GetEscuelasMasCercanas(
            [FromQuery] double latitud, [FromQuery] double longitud, [FromQuery] int cantidad = 5)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            if (cantidad <= 0)
                return BadRequest("La cantidad debe ser mayor a 0.");

            var escuelas = await _geolocationService.GetEscuelasMasCercanasAsync(latitud, longitud, cantidad);
            return Ok(escuelas);
        }

        // GET /Geolocation/partido?latitud=&longitud=
        [HttpGet("partido")]
        public async Task<IActionResult> GetPartidoPorUbicacion([FromQuery] double latitud, [FromQuery] double longitud)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            var partido = await _geolocationService.GetPartidoPorUbicacionAsync(latitud, longitud);
            return partido is null ? NotFound("No se encontro un partido para esa ubicacion.") : Ok(partido);
        }

        // GET /Geolocation/comuna?latitud=&longitud=
        [HttpGet("comuna")]
        public async Task<IActionResult> GetComunaPorUbicacion([FromQuery] double latitud, [FromQuery] double longitud)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            var comuna = await _geolocationService.GetComunaPorUbicacionAsync(latitud, longitud);
            return comuna is null ? NotFound("No se encontro una comuna para esa ubicacion.") : Ok(comuna);
        }

        // GET /Geolocation/direccion?latitud=&longitud=
        // Place name + address of a point picked on the map (reverse geocoding)
        [HttpGet("direccion")]
        public async Task<IActionResult> GetDireccionPorUbicacion(
            [FromQuery] double latitud, [FromQuery] double longitud, CancellationToken cancellationToken)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            try
            {
                var direccion = await _geolocationService.GetDireccionPorUbicacionAsync(latitud, longitud, cancellationToken);
                return direccion is null ? NotFound("No se encontro una direccion para esa ubicacion.") : Ok(direccion);
            }
            catch (HttpRequestException)
            {
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
