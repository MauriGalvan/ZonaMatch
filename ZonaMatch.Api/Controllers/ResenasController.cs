using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Resenas;

namespace ZonaMatch.Api.Controllers
{
    // Resenas de los vecinos sobre una zona. Leer es publico; escribir pide un usuario con sesion iniciada (token Bearer).
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/resenas")]
    public class ResenasController : ControllerBase
    {
        private readonly ListarResenas _listarResenas;
        private readonly GuardarMiResena _guardarMiResena;
        private readonly EliminarMiResena _eliminarMiResena;
        private readonly MarcarResenaUtil _marcarResenaUtil;

        public ResenasController(
            ListarResenas listarResenas,
            GuardarMiResena guardarMiResena,
            EliminarMiResena eliminarMiResena,
            MarcarResenaUtil marcarResenaUtil)
        {
            _listarResenas = listarResenas;
            _guardarMiResena = guardarMiResena;
            _eliminarMiResena = eliminarMiResena;
            _marcarResenaUtil = marcarResenaUtil;
        }

        // GET /Zonas/villa-luro/resenas
        // Con token, cada resena indica si es tuya (esMia) y si la marcaste como util
        [HttpGet]
        [ProducesResponseType<ResenasZonaDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(string slug, CancellationToken cancellationToken) =>
            Ok(await _listarResenas.EjecutarAsync(slug, User.UsuarioId(), cancellationToken));

        // PUT /Zonas/villa-luro/resenas/mia
        // Cuerpo: { "aspectos": { "seguridad": 3, "transporte": 5, "conectividad": 4, "comercios": 4, "espaciosVerdes": 3 },
        //         "texto": "...", "aniosEnZona": 9, "temas": ["transporte"] }
        // Crea tu resena de la zona o la reemplaza (una por usuario y zona). El puntaje general es el promedio de los aspectos.
        [Authorize]
        [HttpPut("mia")]
        [ProducesResponseType<ResenaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GuardarMia(string slug, GuardarResenaDto solicitud, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            return Ok(await _guardarMiResena.EjecutarAsync(slug, sesion, solicitud, cancellationToken));
        }

        // DELETE /Zonas/villa-luro/resenas/mia
        [Authorize]
        [HttpDelete("mia")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EliminarMia(string slug, CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            await _eliminarMiResena.EjecutarAsync(slug, usuarioId, cancellationToken);
            return NoContent();
        }

        // PUT /Zonas/villa-luro/resenas/{id}/util     marca como util una resena de otro usuario
        // DELETE /Zonas/villa-luro/resenas/{id}/util  quita la marca (las dos son idempotentes)
        [Authorize]
        [HttpPut("{id:guid}/util")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public Task<IActionResult> MarcarUtil(string slug, Guid id, CancellationToken cancellationToken) =>
            CambiarUtilAsync(slug, id, util: true, cancellationToken);

        [Authorize]
        [HttpDelete("{id:guid}/util")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> DesmarcarUtil(string slug, Guid id, CancellationToken cancellationToken) =>
            CambiarUtilAsync(slug, id, util: false, cancellationToken);

        private async Task<IActionResult> CambiarUtilAsync(string slug, Guid id, bool util, CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            await _marcarResenaUtil.EjecutarAsync(slug, id, usuarioId, util, cancellationToken);
            return NoContent();
        }
    }
}
