using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Api.Controllers
{
    // Zones (barrios, comunas, localidades, partidos) taken from the OpenStreetMap administrative boundaries
    [ApiController]
    [Route("[controller]")]
    public class ZonasController : ControllerBase
    {
        private const string GeoJson = "application/geo+json";

        private readonly IZonaService _zonaService;

        public ZonasController(IZonaService zonaService)
        {
            _zonaService = zonaService;
        }

        // GET /Zonas/villa-luro?tolerancia=0.00015
        // GeoJSON Feature: boundary as geometry; nombre, nivelAdministrativo, jurisdicciones (containing boundaries,
        // most specific first, each with nombre and nivelAdministrativo), superficieKm2 and centro as properties
        [HttpGet("{slug}")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> GetPorSlug(
            string slug, [FromQuery] double? tolerancia, CancellationToken cancellationToken)
        {
            if (!SlugZona.EsValido(slug))
                return BadRequest("El identificador de zona debe tener minusculas sin acentos separadas por guiones (p. ej. villa-luro).");

            var zona = await _zonaService.GetPorSlugAsync(slug, tolerancia, cancellationToken);
            return zona is null ? NotFound("No se encontro una zona con ese identificador.") : Content(zona, GeoJson);
        }
    }
}
