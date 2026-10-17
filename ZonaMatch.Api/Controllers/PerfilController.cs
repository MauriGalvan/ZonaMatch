using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Perfil;

namespace ZonaMatch.Api.Controllers
{
    // Perfil del usuario: su criterio base (hogar, prioridades, lugares y alquiler), el que usan todos sus analisis
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class PerfilController : ControllerBase
    {
        private readonly ObtenerMiPerfil _obtenerMiPerfil;
        private readonly GuardarMiPerfil _guardarMiPerfil;

        public PerfilController(ObtenerMiPerfil obtenerMiPerfil, GuardarMiPerfil guardarMiPerfil)
        {
            _obtenerMiPerfil = obtenerMiPerfil;
            _guardarMiPerfil = guardarMiPerfil;
        }

        // GET /Perfil — 404 si todavia no lo armo
        [HttpGet]
        [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            // Sin perfil, el mismo 404 que armaria el manejador global, pero sin lanzar una excepcion
            return await _obtenerMiPerfil.EjecutarAsync(usuarioId, cancellationToken) is { } perfil
                ? Ok(perfil)
                : Problem(statusCode: StatusCodes.Status404NotFound, title: PerfilNoEncontradoException.Mensaje);
        }

        // PUT /Perfil
        // Body: { "contexto": {}, "criterios": {}, "puntos": {} } — lo crea o lo reemplaza completo
        [HttpPut]
        [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Guardar(CriterioUsuarioRequest solicitud, CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            return Ok(await _guardarMiPerfil.EjecutarAsync(usuarioId, solicitud, cancellationToken));
        }
    }
}
