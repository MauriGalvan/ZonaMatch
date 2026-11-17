using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.Controllers;

// Admin panel: reviews to moderate and the history of resolved ones. Approving and rejecting stay in
// ReviewModerationController (PUT /api/v1/zones/{slug}/reviews/{id}/resolution).
[ApiController]
[Route("api/v1/admin/reviews")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminReviewsController : ControllerBase
{
    // GET /api/v1/admin/reviews
    [HttpGet]
    [ProducesResponseType<AdminReviewListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromServices] ListAdminReviews useCase, CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(cancellationToken));
    }
}
