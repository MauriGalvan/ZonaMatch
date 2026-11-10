using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.AnalysisFollowUp.Requests;
using ZonaMatch.Application.DTOs.AnalysisFollowUp.Responses;
using ZonaMatch.Application.UseCases.AnalysisFollowUp;

namespace ZonaMatch.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/analysis-follow-ups")]
public sealed class AnalysisFollowUpsController : ControllerBase
{
    [HttpPost("invitations")]
    [ProducesResponseType<AnalysisFollowUpResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> InviteAsync([FromServices] InviteAnalysisFollowUp inviteAnalysisFollowUp, CancellationToken cancellationToken)
    {
        var invitation = await inviteAnalysisFollowUp.ExecuteAsync(User.RequireUserId(), cancellationToken);
        return invitation is null ? NoContent() : Ok(invitation);
    }

    [HttpPost("{id:guid}/responses")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SubmitAsync(Guid id, [FromBody] SubmitAnalysisFollowUpRequest request,
        [FromServices] SubmitAnalysisFollowUp submitAnalysisFollowUp, CancellationToken cancellationToken)
    {
        await submitAnalysisFollowUp.ExecuteAsync(id, User.RequireUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/postponements")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PostponeAsync(Guid id, [FromServices] PostponeAnalysisFollowUp postponeAnalysisFollowUp, CancellationToken cancellationToken)
    {
        await postponeAnalysisFollowUp.ExecuteAsync(id, User.RequireUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("preferences")]
    [ProducesResponseType<FeedbackPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferencesAsync([FromServices] GetFeedbackPreferences getFeedbackPreferences, CancellationToken cancellationToken) =>
        Ok(await getFeedbackPreferences.ExecuteAsync(User.RequireUserId(), cancellationToken));

    [HttpPut("preferences")]
    [ProducesResponseType<FeedbackPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreferencesAsync([FromBody] UpdateFeedbackPreferencesRequest request,
        [FromServices] UpdateFeedbackPreferences updateFeedbackPreferences, CancellationToken cancellationToken) =>
        Ok(await updateFeedbackPreferences.ExecuteAsync(User.RequireUserId(), request.SurveysEnabled!.Value, cancellationToken));
}
