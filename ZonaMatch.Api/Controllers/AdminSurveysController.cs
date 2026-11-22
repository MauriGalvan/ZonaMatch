using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Admin.Requests;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.Controllers;

[ApiController]
[Route("api/v1/admin/surveys")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminSurveysController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminSurveyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminSurveyQuery query,
        [FromServices] ListAdminSurveys useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(query, cancellationToken));
}
