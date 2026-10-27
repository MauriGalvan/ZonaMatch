using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.ZoneRecommendations.Requests;
using ZonaMatch.Application.DTOs.ZoneRecommendations.Responses;
using ZonaMatch.Application.UseCases.ZoneRecommendations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Api.Controllers;

[ApiController]
[Route("api/v1/zones")]
public sealed class ZoneProfileRecommendationsController : ControllerBase
{
    // GET /api/v1/zones/{slug}/profile-recommendations?ageRanges=0-2,6-11&hasPets=true&hasBicycle=true
    [HttpGet("{slug}/profile-recommendations")]
    [ProducesResponseType<ZoneProfileRecommendationsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ZoneProfileRecommendationsResponse>> GetAsync(
        [FromRoute] string slug,
        [FromQuery] GetZoneProfileRecommendationsRequest request,
        [FromServices] GetZoneProfileRecommendations useCase,
        CancellationToken cancellationToken)
    {
        if (!SlugZona.EsValido(slug))
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "The zone slug is not valid.");

        return Ok(await useCase.ExecuteAsync(slug, request, cancellationToken));
    }
}
