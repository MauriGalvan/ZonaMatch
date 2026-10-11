using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Zonas;
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
        private readonly ObtenerIndicadoresZona _obtenerIndicadores;
        private readonly ObtenerTransporteZona _obtenerTransporte;

        public ZonasController(
            IZonaService zonaService, ObtenerIndicadoresZona obtenerIndicadores, ObtenerTransporteZona obtenerTransporte)
        {
            _zonaService = zonaService;
            _obtenerIndicadores = obtenerIndicadores;
            _obtenerTransporte = obtenerTransporte;
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
                return BadRequest(SlugInvalido);

            var zona = await _zonaService.GetPorSlugAsync(slug, tolerancia, cancellationToken);
            return zona is null ? NotFound("No se encontro una zona con ese identificador.") : Content(zona, GeoJson);
        }

        // GET /Zonas/villa-luro/puntos-interes?categorias=transporte,salud
        // Points inside the zone, closest to its center first (DistanciaMetros is measured from the center).
        // Without categorias it returns every category; an unknown zone gives an empty list.
        [HttpGet("{slug}/puntos-interes")]
        public async Task<IActionResult> GetPuntosInteres(
            string slug, [FromQuery] string? categorias, CancellationToken cancellationToken)
        {
            if (!SlugZona.EsValido(slug))
                return BadRequest(SlugInvalido);

            if (!CategoriaPuntoInteres.TryParseLista(categorias, out var codigos, out var error))
                return BadRequest(error);

            var puntos = await _zonaService.GetPuntosInteresAsync(slug, codigos, cancellationToken);
            return Ok(puntos);
        }

        // GET /Zonas/villa-luro/puntos-interes/resumen
        [HttpGet("{slug}/puntos-interes/resumen")]
        public async Task<IActionResult> GetResumenPuntosInteres(string slug, CancellationToken cancellationToken)
        {
            if (!SlugZona.EsValido(slug))
                return BadRequest(SlugInvalido);

            var resumen = await _zonaService.GetResumenPuntosInteresAsync(slug, cancellationToken);
            return Ok(resumen);
        }

        // GET /Zonas/villa-luro/indicadores
        // Poblacion, conectividad, condiciones de vida (Censo 2022) y seguridad (Mapa del Delito, solo CABA),
        // con la fuente y el nivel geografico de cada dato. 404 si la zona no tiene datos cargados.
        [HttpGet("{slug}/indicadores")]
        [ResponseCache(Duration = 3600)]
        [ProducesResponseType<IndicadoresZonaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetIndicadores(string slug, CancellationToken cancellationToken)
        {
            if (!SlugZona.EsValido(slug))
                return BadRequest(SlugInvalido);

            return Ok(await _obtenerIndicadores.EjecutarAsync(slug, cancellationToken));
        }

        // GET /Zonas/villa-luro/transporte
        // Colectivos, trenes y subte de un dia habil: paradas, lineas y salidas por hora en cada franja horaria
        // (madrugada, manana, mediodia, tarde y noche), en total y por linea. Sale de los GTFS publicos.
        [HttpGet("{slug}/transporte")]
        [ResponseCache(Duration = 3600)]
        [ProducesResponseType<TransporteZonaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransporte(string slug, CancellationToken cancellationToken)
        {
            if (!SlugZona.EsValido(slug))
                return BadRequest(SlugInvalido);

            return Ok(await _obtenerTransporte.EjecutarAsync(slug, cancellationToken));
        }

        private const string SlugInvalido =
            "El identificador de zona debe tener minusculas sin acentos separadas por guiones (p. ej. villa-luro).";
    }
}
