using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Nps;
using ZonaMatch.Application.UseCases.Nps;

namespace ZonaMatch.Api.Controllers;

[ApiController, Authorize, Route("api/v1/nps")]
public sealed class NpsController : ControllerBase
{
    [HttpPost("invitations")]
    [ProducesResponseType<NpsInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> InviteAsync([FromServices] NpsSurveys surveys, CancellationToken cancellationToken)
    {
        var invitation = await surveys.InviteAsync(User.RequireUserId(), cancellationToken);
        return invitation is null ? NoContent() : Ok(invitation);
    }

    [HttpPost("{id:guid}/responses")]
    public async Task<IActionResult> SubmitAsync(Guid id, [FromBody] SubmitNpsRequest request, [FromServices] NpsSurveys surveys, CancellationToken cancellationToken)
    {
        await surveys.SubmitAsync(id, User.RequireUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/postponements")]
    public async Task<IActionResult> PostponeAsync(Guid id, [FromServices] NpsSurveys surveys, CancellationToken cancellationToken)
    {
        await surveys.PostponeAsync(id, User.RequireUserId(), cancellationToken);
        return NoContent();
    }
}
