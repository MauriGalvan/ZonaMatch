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

        public GroupController(CreateGroup createGroup, GetGroups getGroups)
        {
            _createGroup = createGroup;
            _getGroups = getGroups;
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

            if (!Guid.TryParse(idClaim, out var userCreatorId))
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            // 2. Le pasamos el trabajo al Servicio
            var nuevoGrupo = await _createGroup.ExecuteAsync(request, userCreatorId, cancellationToken);

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

            if (!Guid.TryParse(idClaim, out var userCreatorId))
                return Unauthorized(new ProblemDetails { Title = "Usuario inválido." });

            // 2. Le pasamos el trabajo al Servicio
            var response = await _getGroups.ExecuteAsync(userCreatorId, cancellationToken);

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

        // DELETE api/<GroupController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
