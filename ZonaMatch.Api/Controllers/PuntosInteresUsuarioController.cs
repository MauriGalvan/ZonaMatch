using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.PuntosInteres;

namespace ZonaMatch.Api.Controllers
{
    // Puntos de interes personales del usuario autenticado (pantalla "Ajuste de criterios"): trabajo, colegio,
    // casa de un amigo... Son privados; no tienen relacion con los puntos del mapa de /Zonas y /Geolocation.
    [ApiController]
    [Authorize]
    [Route("Usuarios/me/puntos-interes")]
    public class PuntosInteresUsuarioController : ControllerBase
    {
        private readonly CrearMiPuntoInteres _crear;
        private readonly ListarMisPuntosInteres _listar;

        public PuntosInteresUsuarioController(CrearMiPuntoInteres crear, ListarMisPuntosInteres listar)
        {
            _crear = crear;
            _listar = listar;
        }

        // GET /Usuarios/me/puntos-interes
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<PuntoInteresUsuarioDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Listar(CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            return Ok(await _listar.EjecutarAsync(usuarioId, cancellationToken));
        }

        // POST /Usuarios/me/puntos-interes
        // Cuerpo: { "tag": "Trabajo", "alias": "Oficina", "latitud": -34.62, "longitud": -58.44 }
        // "alias" es opcional. Direccion y barrio se calculan a partir de las coordenadas.
        [HttpPost]
        [ProducesResponseType<PuntoInteresUsuarioDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Crear(CrearPuntoInteresUsuarioDto solicitud, CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            var punto = await _crear.EjecutarAsync(usuarioId, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, punto);
        }
    }
}
