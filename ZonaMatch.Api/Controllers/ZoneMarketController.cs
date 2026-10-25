using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.ZoneMarket.Responses;
using ZonaMatch.Application.UseCases.Zonas;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("api/v1/zones")]
    public class ZoneMarketController : ControllerBase
    {
        // GET /api/v1/zones/villa-crespo/market
        // Rent and sale of the zone from the Zonaprop listings: averages, price per m2, expenses and listings, by type,
        // with the source and the comuna or partido to compare. 404 when there is no data for the zone.
        [HttpGet("{slug}/market")]
        [ResponseCache(Duration = 3600)]
        [ProducesResponseType<ZoneMarketResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ZoneMarketResponse>> GetAsync(
            [FromRoute] string slug,
            [FromServices] GetZoneMarket useCase,
            CancellationToken cancellationToken)
        {
            if (!SlugZona.EsValido(slug))
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "The zone slug is not valid.");

            return Ok(await useCase.ExecuteAsync(slug, cancellationToken));
        }
    }
}
