using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.UseCases.Aportes;

namespace ZonaMatch.Api.Controllers
{
    // Site administrators resolve community contributions without waiting for the votes
    [ApiController]
    [Route("api/v1/zones/{slug:regex(" + RutasZona.Slug + ")}/contributions")]
    [Authorize(Roles = AppRoles.Admin)]
    public class ContributionModerationController : ControllerBase
    {
        // PUT /api/v1/zones/villa-luro/contributions/{id}/resolution    Body: { "approve": true }
        // An approved contribution is persisted as a point of interest right away.
        [HttpPut("{id:guid}/resolution")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Resolve(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            [FromBody] ResolveContributionRequest request,
            [FromServices] ResolveContribution useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } administratorId)
                return Unauthorized();

            var contribution = await useCase.ExecuteAsync(slug, id, administratorId, request.Approve!.Value, cancellationToken);
            return Ok(contribution);
        }
    }
}
