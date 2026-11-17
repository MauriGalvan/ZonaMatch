using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.Controllers;

// Admin panel: site statistics read from the database. Only site administrators can access it.
[ApiController]
[Route("api/v1/admin/stats")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminStatsController : ControllerBase
{
    // GET /api/v1/admin/stats
    [HttpGet]
    [ProducesResponseType<AdminStatsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync([FromServices] GetAdminStats useCase, CancellationToken cancellationToken)
    {
        var stats = await useCase.ExecuteAsync(cancellationToken);
        return Ok(stats);
    }

    // GET /api/v1/admin/stats/groups
    [HttpGet("groups")]
    [ProducesResponseType<IReadOnlyList<AdminGroupResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListGroupsAsync([FromServices] ListAdminGroups useCase, CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(cancellationToken));
    }
}
