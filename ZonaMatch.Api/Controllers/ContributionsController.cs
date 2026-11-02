using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Contribution.Requests;
using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.UseCases.Contribution;

namespace ZonaMatch.Api.Controllers;

// Community contributions to a zone map stay pending until other users validate them.
[ApiController]
[Route("api/v1/zones/{slug:regex(" + ZoneRouteConstraints.Slug + ")}/contributions")]
public sealed class ContributionsController : ControllerBase
{
    // GET /api/v1/zones/villa-luro/contributions/summary
    [HttpGet("summary")]
    [ProducesResponseType<ContributionSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryAsync(
        [FromRoute] string slug,
        [FromServices] GetContributionSummary useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(slug, cancellationToken));

    // GET /api/v1/zones/villa-luro/contributions/pending
    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<ContributionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingAsync(
        [FromRoute] string slug,
        [FromServices] ListPendingContributions useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(slug, User.GetAuthenticatedUserId(), cancellationToken));

    // POST /api/v1/zones/villa-luro/contributions/points
    // Body: { "category": "salud", "name": "", "schedule": "", "address": "", "latitude": -34.63,
    //         "longitude": -58.50, "livesOrWorksInZone": true }
    [Authorize]
    [HttpPost("points")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePointAsync(
        [FromRoute] string slug,
        [FromBody] CreatePointContributionRequest request,
        [FromServices] CreatePointContribution useCase,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserContext() is not { } currentUser)
            return Unauthorized();

        var contribution = await useCase.ExecuteAsync(slug, currentUser, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, contribution);
    }

    // POST /api/v1/zones/villa-luro/contributions/corrections
    // Body: { "pointOfInterestId": "n123", "pointOfInterestName": "", "reason": "cerro|nombre|ubicacion|categoria|otro",
    //         "proposedName": "", "proposedCategory": "", "latitude": 0, "longitude": 0, "comment": "" }
    [Authorize]
    [HttpPost("corrections")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCorrectionAsync(
        [FromRoute] string slug,
        [FromBody] CreateContributionCorrectionRequest request,
        [FromServices] CreateContributionCorrection useCase,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserContext() is not { } currentUser)
            return Unauthorized();

        var contribution = await useCase.ExecuteAsync(slug, currentUser, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, contribution);
    }

    // PUT /api/v1/zones/villa-luro/contributions/{id}/validation Body: { "confirm": true }
    // Cast or change your vote on another user's pending contribution.
    [Authorize]
    [HttpPut("{id:guid}/validation")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ValidateAsync(
        [FromRoute] string slug,
        [FromRoute] Guid id,
        [FromBody] VoteOnContributionRequest request,
        [FromServices] ValidateContribution useCase,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserId() is not { } userId)
            return Unauthorized();

        var contribution = await useCase.ExecuteAsync(slug, id, userId, request, cancellationToken);
        return Ok(contribution);
    }
}
