using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Nps;
using ZonaMatch.Application.UseCases.Nps;

namespace ZonaMatch.Api.Controllers;

[ApiController, Route("api/v1/admin/nps"), Authorize(Roles = AppRoles.Admin)]
public sealed class AdminNpsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminNpsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminNpsQuery query, [FromServices] ListAdminNps useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(query, cancellationToken));
}
