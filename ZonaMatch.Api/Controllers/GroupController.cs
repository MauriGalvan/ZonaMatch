using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ZonaMatch.Application.UseCases;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Application.DTOs.Group;
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
        private readonly DeleteGroup _deleteGroupUseCase;

        public GroupController(CreateGroup createGroup, GetGroups getGroups, DeleteGroup deleteGroupUseCase)
        {
            _createGroup = createGroup;
            _getGroups = getGroups;
            _deleteGroupUseCase = deleteGroupUseCase;
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

        // GET api/<GroupController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // PUT api/<GroupController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
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
                await _deleteGroupUseCase.ExecuteAsync(groupId, userId, cancellationToken);
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
    }
}
