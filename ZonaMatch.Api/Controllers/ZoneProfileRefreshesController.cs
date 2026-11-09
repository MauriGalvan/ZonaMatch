using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.UseCases.Zone;

namespace ZonaMatch.Api.Controllers;

// Administrators only: after loading data with the ETL (for example ZONE-MARKET-DOCKER.bat), rebuilds the zone
// profiles of this API instance instead of restarting it.
[ApiController]
[Route("api/v1/zone-profile-refreshes")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class ZoneProfileRefreshesController : ControllerBase
{
    // POST /api/v1/zone-profile-refreshes
    [HttpPost]
    [ProducesResponseType<ZoneProfilesRefreshResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ZoneProfilesRefreshResponse>> CreateAsync(
        [FromServices] RefreshZoneProfiles useCase,
        CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(cancellationToken));
    }
}
