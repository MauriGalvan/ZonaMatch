using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.UseCases.Review;

namespace ZonaMatch.Api.Controllers;

// Site administrators approve or reject community reviews. Until then a review, and its images, is visible only to its author.
[ApiController]
[Route("api/v1")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class ReviewModerationController : ControllerBase
{
    // GET /api/v1/reviews/pending
    // Reviews of every zone waiting for approval, oldest first.
    [HttpGet("reviews/pending")]
    [ProducesResponseType<IReadOnlyList<PendingReviewResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPendingAsync(
        [FromServices] ListPendingReviews useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    // PUT /api/v1/zones/villa-luro/reviews/{id}/resolution    Body: { "approve": false, "reason": "..." }
    // The reason is required when rejecting; the author sees it next to their review.
    [HttpPut("zones/{slug:regex(" + ZoneRouteConstraints.Slug + ")}/reviews/{id:guid}/resolution")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveAsync(
        [FromRoute] string slug,
        [FromRoute] Guid id,
        [FromBody] ResolveReviewRequest request,
        [FromServices] ResolveReview useCase,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserId() is not { } administratorId)
            return Unauthorized();

        await useCase.ExecuteAsync(slug, id, administratorId, request.Approve!.Value, request.Reason, cancellationToken);
        return NoContent();
    }
}
