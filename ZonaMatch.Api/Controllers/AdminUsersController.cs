using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Admin.Requests;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.Controllers;

// Admin panel: user management. Only site administrators can access it.
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    // GET /api/v1/admin/users
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromServices] ListUsers useCase, CancellationToken cancellationToken)
    {
        var users = await useCase.ExecuteAsync(cancellationToken);
        return Ok(users);
    }

    // PUT /api/v1/admin/users/{id}/role    Body: { "isAdmin": true }
    // The user sees the new role after signing in again, because the role travels in the access token.
    [HttpPut("{id:guid}/role")]
    [ProducesResponseType<AdminUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoleAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRoleRequest request,
        [FromServices] UpdateUserRole useCase,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserId() is not { } administratorId)
            return Unauthorized();

        var user = await useCase.ExecuteAsync(id, request.IsAdmin!.Value, administratorId, cancellationToken);
        return Ok(user);
    }
}
