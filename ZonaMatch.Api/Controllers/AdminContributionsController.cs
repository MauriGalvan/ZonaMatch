using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.Controllers;

// Admin panel: community contributions to moderate and the history of resolved ones. Approving and rejecting stay in
// ContributionModerationController (PUT /api/v1/zones/{slug}/contributions/{id}/resolution).
[ApiController]
[Route("api/v1/admin/contributions")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminContributionsController : ControllerBase
{
    // GET /api/v1/admin/contributions
    [HttpGet]
    [ProducesResponseType<AdminContributionListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromServices] ListAdminContributions useCase, CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(cancellationToken));
    }
}
