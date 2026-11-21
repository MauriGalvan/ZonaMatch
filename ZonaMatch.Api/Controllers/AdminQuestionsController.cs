using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.Controllers;

// Admin panel: follow the community questions and delete an inappropriate one.
[ApiController]
[Route("api/v1/admin/questions")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminQuestionsController : ControllerBase
{
    // GET /api/v1/admin/questions
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminQuestionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromServices] ListAdminQuestions useCase, CancellationToken cancellationToken)
    {
        return Ok(await useCase.ExecuteAsync(cancellationToken));
    }

    // DELETE /api/v1/admin/questions/{id}: deletes the question and its answers.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid id,
        [FromServices] DeleteAdminQuestion useCase,
        CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, User.RequireUserId(), cancellationToken);
        return NoContent();
    }
}
