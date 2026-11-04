using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Profile.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Profile;

namespace ZonaMatch.Api.Controllers;

// User profile: the base criteria used by every analysis.
[ApiController]
[Route("api/v1/profile")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    // GET /api/v1/profile returns 404 when the user has not created a profile yet.
    [HttpGet]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileResponse>> Get(
        [FromServices] GetMyProfile getMyProfile,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserId() is not { } userId)
            return Unauthorized();

        var profile = await getMyProfile.ExecuteAsync(userId, cancellationToken);
        if (profile is null)
            throw new NotFoundException("Todavía no armaste tu perfil.");

        return Ok(profile);
    }

    // PUT /api/v1/profile creates or fully replaces the profile.
    [HttpPut]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> Save(
        [FromBody] AnalysisCriteriaRequest request,
        [FromServices] SaveMyProfile saveMyProfile,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserId() is not { } userId)
            return Unauthorized();

        return Ok(await saveMyProfile.ExecuteAsync(userId, request, cancellationToken));
    }
}
