using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.UseCases.Review;

namespace ZonaMatch.Api.Controllers;

// Community reviews for a zone. Reading is public; writing requires an authenticated user.
[ApiController]
[Route("api/v1/zones/{slug:regex(" + ZoneRouteConstraints.Slug + ")}/reviews")]
public sealed class ReviewsController : ControllerBase
{
    // GET /api/v1/zones/villa-luro/reviews
    // With a token, each review indicates whether it is yours and whether you marked it helpful.
    [HttpGet]
    [ProducesResponseType<ZoneReviewListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        string slug,
        [FromServices] ListZoneReviews listZoneReviews,
        CancellationToken cancellationToken) =>
        Ok(await listZoneReviews.ExecuteAsync(slug, User.GetAuthenticatedUserId(), User.IsInRole(AppRoles.Admin), cancellationToken));

    // PUT /api/v1/zones/villa-luro/reviews/mine
    // Body: { "aspects": { "safety": 3, "transport": 5, "connectivity": 4, "commerce": 4, "greenSpaces": 3 },
    //         "text": "...", "yearsInZone": 9, "topics": ["transporte"], "imageNames": ["<name from POST /api/v1/images>"] }
    // Creates or replaces your review for the zone (one per user and zone), with up to 3 images.
    [Authorize]
    [HttpPut("mine")]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveMineAsync(
        string slug,
        [FromBody] SaveReviewRequest request,
        [FromServices] SaveMyReview saveMyReview,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserContext() is not { } currentUser)
            return Unauthorized();

        return Ok(await saveMyReview.ExecuteAsync(slug, currentUser, request, cancellationToken));
    }

    // DELETE /api/v1/zones/villa-luro/reviews/mine
    [Authorize]
    [HttpDelete("mine")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMineAsync(
        string slug,
        [FromServices] DeleteMyReview deleteMyReview,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserId() is not { } userId)
            return Unauthorized();

        await deleteMyReview.ExecuteAsync(slug, userId, cancellationToken);
        return NoContent();
    }

    // PUT marks another user's review as helpful; DELETE removes the mark. Both operations are idempotent.
    [Authorize]
    [HttpPut("{id:guid}/helpful-votes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> MarkHelpfulAsync(
        string slug,
        Guid id,
        [FromServices] SetReviewHelpful setReviewHelpful,
        CancellationToken cancellationToken) =>
        ChangeHelpfulAsync(slug, id, helpful: true, setReviewHelpful, cancellationToken);

    [Authorize]
    [HttpDelete("{id:guid}/helpful-votes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UnmarkHelpfulAsync(
        string slug,
        Guid id,
        [FromServices] SetReviewHelpful setReviewHelpful,
        CancellationToken cancellationToken) =>
        ChangeHelpfulAsync(slug, id, helpful: false, setReviewHelpful, cancellationToken);

    private async Task<IActionResult> ChangeHelpfulAsync(
        string slug,
        Guid reviewId,
        bool helpful,
        SetReviewHelpful setReviewHelpful,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserId() is not { } userId)
            return Unauthorized();

        await setReviewHelpful.ExecuteAsync(slug, reviewId, userId, helpful, cancellationToken);
        return NoContent();
    }
}
