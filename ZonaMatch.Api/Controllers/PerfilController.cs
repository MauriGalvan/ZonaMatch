using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Perfil;

namespace ZonaMatch.Api.Controllers
{
    // User profile: the base criteria (home, priorities, places, and rent) used by all analyses.
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class PerfilController : ControllerBase
    {
        // GET /Perfil returns 404 if the user has not created a profile yet.
        [HttpGet]
        [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Obtener(
            [FromServices] ObtenerMiPerfil obtenerMiPerfil,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            var profile = await obtenerMiPerfil.EjecutarAsync(usuarioId, cancellationToken);
            if (profile is null)
                throw new NotFoundException("Todavía no armaste tu perfil.");

            return Ok(profile);
        }

        // PUT /Perfil
        // Body: { "contexto": {}, "criterios": {}, "puntos": {} } creates or fully replaces the profile.
        [HttpPut]
        [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Guardar(
            [FromBody] CriterioUsuarioRequest solicitud,
            [FromServices] GuardarMiPerfil guardarMiPerfil,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            return Ok(await guardarMiPerfil.EjecutarAsync(usuarioId, solicitud, cancellationToken));
        }
    }
}
