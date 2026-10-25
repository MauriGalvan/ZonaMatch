using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.Routes.Requests;
using ZonaMatch.Application.DTOs.Routes.Responses;
using ZonaMatch.Application.UseCases.Routes;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("api/v1/routes")]
    public class RoutesController : ControllerBase
    {
        // GET /api/v1/routes?fromLatitude=-34.65&fromLongitude=-58.62&toLatitude=-34.60&toLongitude=-58.38&mode=transporte-publico
        // Fastest trip between two points with its legs (line of each public transport leg and the path to draw).
        // 404 when there is no trip for that mode; 503 when the routing service does not answer.
        [HttpGet]
        [ProducesResponseType<RouteResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<RouteResponse>> GetAsync(
            [FromQuery] RouteRequest request,
            [FromServices] GetRoute useCase,
            CancellationToken cancellationToken)
        {
            return Ok(await useCase.ExecuteAsync(request, cancellationToken));
        }
    }
}
