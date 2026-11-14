using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.Controllers;

// Admin panel: data of every zone, scored the same way as the analysis. Only site administrators can access it.
[ApiController]
[Route("api/v1/admin/zones")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminZonesController : ControllerBase
{
    // GET /api/v1/admin/zones
    [HttpGet]
    [ProducesResponseType<AdminZoneOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromServices] ListAdminZones useCase, CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(cancellationToken));
    }

    // GET /api/v1/admin/zones/palermo
    [HttpGet("{slug:regex(" + ZoneRouteConstraints.Slug + ")}")]
    [ProducesResponseType<AdminZoneDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync([FromRoute] string slug, [FromServices] GetAdminZone useCase, CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(slug, cancellationToken));
    }
}
