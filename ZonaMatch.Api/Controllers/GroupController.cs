using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;
using ZonaMatch.Application.DTOs.GroupAnalysis.Requests;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.UseCases.Group;

namespace ZonaMatch.Api.Controllers
{
    [Route("api/v1/groups")]
    [ApiController]
    [Authorize]
    public class GroupController : ControllerBase
    {

        // GET /api/v1/groups/{groupId} returns group details to its participants.
        [HttpGet("{groupId:guid}")]
        [ProducesResponseType<GroupDetailsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupDetailsAsync(
            [FromRoute] Guid groupId,
            [FromServices] GetGroupDetails getGroupDetails,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var response = await getGroupDetails.ExecuteAsync(groupId, userId, cancellationToken);
            return Ok(response);
        }

        // POST /api/v1/groups/{groupId}/rankings calculates the automatic top five and recommendations.
        [HttpPost("{groupId:guid}/rankings")]
        [ProducesResponseType<GroupRankingResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CalculateGroupScoreAsync(
            [FromRoute] Guid groupId,
            [FromBody] CalculateGroupScoreRequest request,
            [FromServices] CalculateGroupScore calculateGroupScore,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var response = await calculateGroupScore.ExecuteAsync(groupId, userId, request, cancellationToken);
            return Ok(response);
        }

        // POST /api/v1/groups/{groupId}/analyses saves the shared result to the current user's analyses.
        [HttpPost("{groupId:guid}/analyses")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SaveGroupAnalysisAsync(
            [FromRoute] Guid groupId,
            [FromBody] SaveGroupAnalysisRequest request,
            [FromServices] SaveGroupAnalysis saveGroupAnalysis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var analysis = await saveGroupAnalysis.ExecuteAsync(groupId, userId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, analysis);
        }

        // POST /api/v1/groups
        // Body: { "name": "Capital search" }
        [HttpPost]
        [ProducesResponseType<GroupResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateGroupAsync(
            [FromBody] CreateGroupRequest request,
            [FromServices] CreateGroup createGroup,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            var group = await createGroup.ExecuteAsync(request, userId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, group);
        }

        // GET /api/v1/groups returns the current user's groups.
        [HttpGet]
        [ProducesResponseType<GroupListResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetGroupsAsync(
            [FromServices] GetGroups getGroups,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            var response = await getGroups.ExecuteAsync(userId, cancellationToken);
            return Ok(response);
        }


        [HttpPut("{groupId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UpdateGroupAsync(
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

        [HttpDelete("{groupId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DeleteGroupAsync(
            [FromRoute] Guid groupId,
            [FromServices] DeleteGroup deleteGroup,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await deleteGroup.ExecuteAsync(groupId, userId, cancellationToken);
            return NoContent();
        }

        // DELETE /api/v1/groups/{groupId}/participants/{userIdToRemove} removes a participant.
        [HttpDelete("{groupId:guid}/participants/{userIdToRemove:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RemoveParticipantAsync(
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
