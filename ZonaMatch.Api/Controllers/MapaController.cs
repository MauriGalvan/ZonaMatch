using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Api.Controllers
{
    // GeoJSON FeatureCollections (RFC 7946) to draw layers on the map
    [ApiController]
    [Route("[controller]")]
    public class MapaController : ControllerBase
    {
        private const string GeoJson = "application/geo+json";

        // GET /Mapa/partidos?tolerancia=0.0005
        [HttpGet("partidos")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> GetPartidos(
            [FromQuery] double? tolerancia,
            [FromServices] IMapaService mapService)
        {
            var geoJson = await mapService.GetPartidosAsync(tolerancia);
            return Content(geoJson, GeoJson);
        }

        // GET /Mapa/comunas?tolerancia=0.0005
        [HttpGet("comunas")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> GetComunas(
            [FromQuery] double? tolerancia,
            [FromServices] IMapaService mapService)
        {
            var geoJson = await mapService.GetComunasAsync(tolerancia);
            return Content(geoJson, GeoJson);
        }

        // GET /Mapa/radios?minLon=-58.5&minLat=-34.7&maxLon=-58.4&maxLat=-34.6
        [HttpGet("radios")]
        public async Task<IActionResult> GetRadios(
            [FromQuery] double minLon, [FromQuery] double minLat, [FromQuery] double maxLon, [FromQuery] double maxLat,
            [FromQuery] double? tolerancia,
            [FromServices] IMapaService mapService)
        {
            var bbox = new BoundingBox(minLon, minLat, maxLon, maxLat);
            if (!bbox.EsValido)
                return BadRequest(ViewportInvalido);

            var geoJson = await mapService.GetRadiosAsync(bbox, tolerancia);
            return geoJson is null ? BadRequest(DemasiadosElementos("radios")) : Content(geoJson, GeoJson);
        }

        // GET /Mapa/escuelas?minLon=&minLat=&maxLon=&maxLat=&nivel=&sector=
        [HttpGet("escuelas")]
        public async Task<IActionResult> GetEscuelas(
            [FromQuery] double minLon, [FromQuery] double minLat, [FromQuery] double maxLon, [FromQuery] double maxLat,
            [FromServices] IMapaService mapService,
            [FromQuery] string? nivel = null, [FromQuery] string? sector = null)
        {
            var bbox = new BoundingBox(minLon, minLat, maxLon, maxLat);
            if (!bbox.EsValido)
                return BadRequest(ViewportInvalido);

            var geoJson = await mapService.GetEscuelasAsync(bbox, nivel, sector);
            return geoJson is null ? BadRequest(DemasiadosElementos("escuelas")) : Content(geoJson, GeoJson);
        }

        private const string ViewportInvalido =
            "El viewport es invalido: indica minLon, minLat, maxLon y maxLat en grados, con min menor que max.";

        private static string DemasiadosElementos(string capa) =>
            $"El viewport contiene mas de {MapaService.MaxFeaturesPorConsulta} {capa}. Acerca el mapa o aplica filtros.";
    }
}
