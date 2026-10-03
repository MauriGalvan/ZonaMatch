using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.Analysis;
using ZonaMatch.Application.PointsOfInterest;
using ZonaMatch.Application.Territory;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("api/territorial-context")]
    [Produces("application/json")]
    public class TerritorialContextController : ControllerBase
    {
        private readonly ZoneQueryService _zones;

        public TerritorialContextController(ZoneQueryService zones)
        {
            _zones = zones;
        }

        /// <summary>Barrio/localidad, comuna/partido y radio censal que contienen al punto (PBI 14).</summary>
        [HttpGet]
        public async Task<ActionResult<TerritorialContextDto>> Get(
            [FromQuery] double lat,
            [FromQuery] double lon,
            CancellationToken cancellationToken)
        {
            return Ok(await _zones.GetContextAsync(lat, lon, cancellationToken));
        }
    }

    [ApiController]
    [Route("api/poi-categories")]
    [Produces("application/json")]
    public class PoiCategoriesController : ControllerBase
    {
        private readonly PoiQueryService _pois;

        public PoiCategoriesController(PoiQueryService pois)
        {
            _pois = pois;
        }

        /// <summary>Taxonomía estándar de puntos de interés (capas y subcategorías).</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<PoiCategoryDto>>> Get(CancellationToken cancellationToken)
        {
            return Ok(await _pois.GetCategoriesAsync(cancellationToken));
        }
    }

    [ApiController]
    [Route("api/analysis")]
    [Produces("application/json")]
    public class AnalysisController : ControllerBase
    {
        private readonly AnalysisService _analysis;

        public AnalysisController(AnalysisService analysis)
        {
            _analysis = analysis;
        }

        /// <summary>Criterios que se pueden evaluar.</summary>
        [HttpGet("criteria")]
        public ActionResult<IReadOnlyList<CriterionInfoDto>> Criteria()
        {
            return Ok(_analysis.GetCriteria());
        }

        /// <summary>Ranking personalizado o combinado de zonas (PBI 34a, 35a, 58a).</summary>
        [HttpPost("ranking")]
        public async Task<ActionResult<RankingResultDto>> Ranking(
            [FromBody] RankingRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _analysis.RankAsync(request, cancellationToken));
        }
    }
}
