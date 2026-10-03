using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.PointsOfInterest;
using ZonaMatch.Application.Territory;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("api/zones")]
    [Produces("application/json")]
    public class ZonesController : ControllerBase
    {
        private readonly ZoneQueryService _zones;
        private readonly PoiQueryService _pois;

        public ZonesController(ZoneQueryService zones, PoiQueryService pois)
        {
            _zones = zones;
            _pois = pois;
        }

        /// <summary>Busca zonas (barrios de CABA y localidades del GBA) por nombre o partido.</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ZoneSummaryDto>>> Search(
            [FromQuery] string? query,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _zones.SearchAsync(query, limit, cancellationToken));
        }

        /// <summary>Información general de la zona, con su polígono y los indicadores disponibles.</summary>
        [HttpGet("{slug}")]
        public async Task<ActionResult<ZoneDetailDto>> Get(string slug, CancellationToken cancellationToken)
        {
            return Ok(await _zones.GetDetailAsync(slug, cancellationToken));
        }

        /// <summary>Zonas aledañas (PBI 53a).</summary>
        [HttpGet("{slug}/neighbors")]
        public async Task<ActionResult<IReadOnlyList<ZoneSummaryDto>>> Neighbors(string slug, CancellationToken cancellationToken)
        {
            return Ok(await _zones.GetNeighborsAsync(slug, cancellationToken));
        }

        /// <summary>Puntos de interés dentro del radio, opcionalmente filtrados por capa (PBI 15-20, 32a).</summary>
        [HttpGet("{slug}/pois")]
        public async Task<ActionResult<IReadOnlyList<PoiDto>>> Pois(
            string slug,
            [FromQuery] int radius = 1000,
            [FromQuery] string? layers = null,
            CancellationToken cancellationToken = default)
        {
            var selectedLayers = (layers ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return Ok(await _pois.GetNearbyAsync(slug, radius, selectedLayers, cancellationToken));
        }

        /// <summary>Conteos por capa y subcategoría dentro del radio (PBI 26a).</summary>
        [HttpGet("{slug}/summary")]
        public async Task<ActionResult<ZonePoiSummaryDto>> Summary(
            string slug,
            [FromQuery] int radius = 1000,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _pois.GetSummaryAsync(slug, radius, cancellationToken));
        }
    }
}
