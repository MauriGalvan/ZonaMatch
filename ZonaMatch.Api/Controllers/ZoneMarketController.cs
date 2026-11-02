using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.ZoneMarket.Responses;
using ZonaMatch.Application.UseCases.Zones;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("api/v1/zones")]
    public class ZoneMarketController : ControllerBase
    {
        // GET /api/v1/zones/villa-crespo/market
        // Rent and sale of the zone from the Zonaprop listings: averages, price per m2, expenses and listings, by type,
        // with the source and the commune or district used for comparison. Returns 404 when the zone has no data.
        [HttpGet("{slug}/market")]
        [ResponseCache(Duration = 3600)]
        [ProducesResponseType<ZoneMarketResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ZoneMarketResponse>> GetZoneMarket(
            [FromRoute] string slug,
            [FromServices] GetZoneMarket getZoneMarket,
            CancellationToken cancellationToken)
        {
            return Ok(await getZoneMarket.ExecuteAsync(slug, cancellationToken));
        }
    }
}
