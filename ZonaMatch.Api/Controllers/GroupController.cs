using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.UseCases.Groups;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ZonaMatch.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GroupController : ControllerBase
    {

        private readonly CreateGroup _createGroup;
        private readonly GetGroups _getGroups;
        private readonly DeleteGroup _deleteGroup;
        private readonly UpdateGroup _updateGroup;

        private readonly RemoveParticipantUseCase _removeParticipant;

        public GroupController(CreateGroup createGroup, GetGroups getGroups, DeleteGroup deleteGroup, UpdateGroup updateGroup, RemoveParticipantUseCase removeParticipant)
        {
            _createGroup = createGroup;
            _getGroups = getGroups;
            _deleteGroup = deleteGroup;
            _updateGroup = updateGroup;
            _removeParticipant = removeParticipant;
        }

        // POST /Group
        // Body: { "name": "Búsqueda Capital" }
        [HttpPost("Create")]
        [ProducesResponseType<GroupResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateGroup(CreateGroupRequest request, CancellationToken cancellationToken)
        {
            // 1. Extraemos el ID del usuario del Token tal como lo hace tu compañero
            var idClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(idClaim, out var userId))
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            // 2. Le pasamos el trabajo al Servicio
            var nuevoGrupo = await _createGroup.ExecuteAsync(request, userId, cancellationToken);

            // 3. Devolvemos 201 Created (Idealmente con la ruta GET para ver el grupo creado, pero Ok/Created está perfecto)
            return StatusCode(StatusCodes.Status201Created, nuevoGrupo);
        }

        // GET: api/<GroupController>
        [HttpGet("Get")]
        [ProducesResponseType<GroupListResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
        {
            // 1. Extraemos el ID del usuario del Token tal como lo hace tu compañero
            var idClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(idClaim, out var userId))
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            // 2. Le pasamos el trabajo al Servicio
            var response = await _getGroups.ExecuteAsync(userId, cancellationToken);

            // 3. Devolvemos 200 OK
            return Ok(response);
        }


        [HttpPut("Update/{groupId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UpdateGroup([FromRoute] Guid groupId, UpdateGroupRequest request, CancellationToken cancellationToken)
        {
            var idClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(idClaim, out var userId))
                return Unauthorized();

            try
            {
                await _updateGroup.ExecuteAsync(groupId, userId, request, cancellationToken);
                return NoContent();
            }
            catch (Exception ex) when (ex.Message == "Grupo no encontrado")
            {
                return NotFound(new ProblemDetails { Title = ex.Message });
            }
            catch (Exception ex) when (ex.Message == "No tienes permiso para modificar este grupo")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = ex.Message });
            }
        }

        [HttpDelete("Delete/{groupId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DeleteGroup([FromRoute] Guid groupId, CancellationToken cancellationToken)
        {
            var idClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(idClaim, out var userId))
                return Unauthorized();

            try
            {
                await _deleteGroup.ExecuteAsync(groupId, userId, cancellationToken);
                return NoContent();
            }
            catch (Exception ex) when (ex.Message == "Grupo no encontrado")
            {
                return NotFound(new ProblemDetails { Title = ex.Message });
            }
            catch (Exception ex) when (ex.Message == "No tienes permiso para eliminar este grupo")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = ex.Message });
            }
        }

        // DELETE: api/Groups/{groupId}/Participants/{userIdToRemove}
        [HttpDelete("{groupId}/Participants/{userIdToRemove}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveParticipant(Guid groupId, Guid userIdToRemove, CancellationToken cancellationToken)
        {
            // Obtienes el ID del usuario logueado desde el token JWT (ajusta esto a tu lógica actual)
            var currentUserId = Guid.Parse(User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException());

            await _removeParticipant.ExecuteAsync(groupId, userIdToRemove, currentUserId, cancellationToken);

            return NoContent(); // 204: Todo salió bien, no hay contenido para devolver
        }

    }
}
