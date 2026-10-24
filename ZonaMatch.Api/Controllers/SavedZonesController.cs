using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.SavedZones.Requests;
using ZonaMatch.Application.DTOs.SavedZones.Responses;
using ZonaMatch.Application.UseCases.SavedZones;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class SavedZonesController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<SavedZoneResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IReadOnlyList<SavedZoneResponse>>> GetAsync(
            [FromServices] ListSavedZones useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            return Ok(await useCase.ExecuteAsync(userId, cancellationToken));
        }

        [HttpPut("{slug}")]
        [ProducesResponseType<SavedZoneResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<SavedZoneResponse>> PutAsync(
            [FromRoute] string slug,
            [FromBody] SaveZoneRequest request,
            [FromServices] SaveZone useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserContext() is not { } user)
                return Unauthorized();

            return Ok(await useCase.ExecuteAsync(slug, user, request, cancellationToken));
        }

        [HttpDelete("{slug}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteAsync(
            [FromRoute] string slug,
            [FromServices] RemoveSavedZone useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await useCase.ExecuteAsync(slug, userId, cancellationToken);
            return NoContent();
        }
    }
}
