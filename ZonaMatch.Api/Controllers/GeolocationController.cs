using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GeolocationController : ControllerBase
    {
        private readonly IGeolocationService _geolocationService;
        private readonly IEscuelaRepository _escuelaRepository;

        public GeolocationController(IGeolocationService geolocationService, IEscuelaRepository escuelaRepository)
        {
            _geolocationService = geolocationService;
            _escuelaRepository = escuelaRepository;
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

        // GET /Geolocation/escuelas?latitud=&longitud=&radioMetros=&nivel=&gestion=
        [HttpGet("escuelas")]
        public async Task<IActionResult> GetEscuelasCercanas(
            [FromQuery] double latitud, [FromQuery] double longitud, [FromQuery] double radioMetros = 1000,
            [FromQuery] string? nivel = null, [FromQuery] string? gestion = null)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            var escuelas = await _geolocationService.GetEscuelasCercanasAsync(latitud, longitud, radioMetros, nivel, gestion);
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

            var resultado = await _escuelaRepository.GetMasCercanosAsync(latitud, longitud, cantidad);

            var respuesta = resultado.Select(r => new
            {
                r.Entidad.Id,
                r.Entidad.Nombre,
                r.Entidad.Nivel,
                r.Entidad.Gestion,
                r.Entidad.Direccion,
                Latitud = r.Entidad.Ubicacion.Y,
                Longitud = r.Entidad.Ubicacion.X,
                r.DistanciaMetros
            });

            return Ok(respuesta);
        }

        // GET /Geolocation/espacios-verdes?latitud=&longitud=&radioMetros=
        [HttpGet("espacios-verdes")]
        public async Task<IActionResult> GetEspaciosVerdesCercanos(
            [FromQuery] double latitud, [FromQuery] double longitud, [FromQuery] double radioMetros = 1000)
        {
            if (!CoordenadasValidas(latitud, longitud, radioMetros, out var error))
                return BadRequest(error);

            var espacios = await _geolocationService.GetEspaciosVerdesCercanosAsync(latitud, longitud, radioMetros);
            return Ok(espacios);
        }

        // GET /Geolocation/barrio?latitud=&longitud=
        [HttpGet("barrio")]
        public async Task<IActionResult> GetBarrioPorUbicacion([FromQuery] double latitud, [FromQuery] double longitud)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            var barrio = await _geolocationService.GetBarrioPorUbicacionAsync(latitud, longitud);
            return barrio is null ? NotFound("No se encontro un barrio para esa ubicacion.") : Ok(barrio);
        }

        // GET /Geolocation/zona?latitud=&longitud=
        [HttpGet("zona")]
        public async Task<IActionResult> GetZonaPorUbicacion([FromQuery] double latitud, [FromQuery] double longitud)
        {
            if (!CoordenadasValidas(latitud, longitud, 0, out var error))
                return BadRequest(error);

            var zona = await _geolocationService.GetZonaPorUbicacionAsync(latitud, longitud);
            return zona is null ? NotFound("No se encontro una zona para esa ubicacion.") : Ok(zona);
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
