using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
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

        // GET /Zonas?buscar=villa l&limite=10
        // Zones whose name contains the text (ignoring case and accents); names that start with it come first
        [HttpGet]
        public async Task<IActionResult> Buscar(
            [FromQuery] string? buscar, [FromQuery] int limite = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(buscar))
                return BadRequest("Indica el texto a buscar en buscar.");

            if (limite is < 1 or > ZonaService.MaxResultadosBusqueda)
                return BadRequest($"El limite debe estar entre 1 y {ZonaService.MaxResultadosBusqueda}.");

            var zonas = await _zonaService.BuscarAsync(buscar, limite, cancellationToken);
            return Ok(zonas);
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
