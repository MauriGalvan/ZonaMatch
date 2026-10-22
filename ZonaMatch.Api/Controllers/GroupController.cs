using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.DTOs.GroupAnalysis.Requests;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.UseCases.Groups;

namespace ZonaMatch.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class GroupController : ControllerBase
    {

        // GET /Group/{groupId} returns group details to its participants.
        [HttpGet("{groupId:guid}")]
        [ProducesResponseType<GroupDetailsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupDetails(
            [FromRoute] Guid groupId,
            [FromServices] GetGroupDetails getGroupDetails,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var response = await getGroupDetails.ExecuteAsync(groupId, userId, cancellationToken);
            return Ok(response);
        }

        // POST /Group/{groupId}/CalcularPuntajeEnGrupo calculates the automatic top five and recommendations.
        [HttpPost("{groupId:guid}/CalcularPuntajeEnGrupo")]
        [ProducesResponseType<GroupRankingResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CalcularPuntajeEnGrupo(
            [FromRoute] Guid groupId,
            [FromBody] CalculateGroupScoreRequest request,
            [FromServices] CalcularPuntajeEnGrupo calcularPuntajeEnGrupo,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var response = await calcularPuntajeEnGrupo.EjecutarAsync(groupId, userId, request, cancellationToken);
            return Ok(response);
        }

        // POST /Group/{groupId}/Analisis saves the shared result to the current user's analyses.
        [HttpPost("{groupId:guid}/Analisis")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GuardarAnalisisEnGrupo(
            [FromRoute] Guid groupId,
            [FromBody] SaveGroupAnalysisRequest request,
            [FromServices] GuardarAnalisisEnGrupo guardarAnalisisEnGrupo,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var analysis = await guardarAnalisisEnGrupo.EjecutarAsync(groupId, userId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, analysis);
        }

        // POST /Group
        // Body: { "name": "Capital search" }
        [HttpPost("Create")]
        [ProducesResponseType<GroupResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateGroup(
            [FromBody] CreateGroupRequest request,
            [FromServices] CreateGroup createGroup,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            var group = await createGroup.ExecuteAsync(request, userId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, group);
        }

        // GET /Group/Get returns the current user's groups.
        [HttpGet("Get")]
        [ProducesResponseType<GroupListResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetGroups(
            [FromServices] GetGroups getGroups,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            var response = await getGroups.ExecuteAsync(userId, cancellationToken);
            return Ok(response);
        }


        [HttpPut("Update/{groupId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UpdateGroup(
            [FromRoute] Guid groupId,
            [FromBody] UpdateGroupRequest request,
            [FromServices] UpdateGroup updateGroup,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await updateGroup.ExecuteAsync(groupId, userId, request, cancellationToken);
            return NoContent();
        }

        [HttpDelete("Delete/{groupId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DeleteGroup(
            [FromRoute] Guid groupId,
            [FromServices] DeleteGroup deleteGroup,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await deleteGroup.ExecuteAsync(groupId, userId, cancellationToken);
            return NoContent();
        }

        // DELETE /Group/{groupId}/Participants/{userIdToRemove} removes a participant.
        [HttpDelete("{groupId}/Participants/{userIdToRemove}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RemoveParticipant(
            Guid groupId,
            Guid userIdToRemove,
            [FromServices] RemoveParticipant removeParticipant,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await removeParticipant.ExecuteAsync(groupId, userIdToRemove, userId, cancellationToken);

            return NoContent();
        }

    }
}
