using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Controllers
{
    // Neighbors' reviews of a zone. Reading is public; writing needs a logged-in user (Bearer token).
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/resenas")]
    public class ResenasController : ControllerBase
    {
        private readonly IResenaService _resenaService;

        public ResenasController(IResenaService resenaService)
        {
            _resenaService = resenaService;
        }

        // GET /Zonas/villa-luro/resenas
        // With a token, each review says whether it is yours (esMia) and whether you marked it useful
        [HttpGet]
        [ProducesResponseType<ResenasZonaDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(string slug, CancellationToken cancellationToken) =>
            Ok(await _resenaService.ListarAsync(slug, User.UsuarioId(), cancellationToken));

        // PUT /Zonas/villa-luro/resenas/mia
        // Body: { "puntaje": 4, "aspectos": { "seguridad": 3, "transporte": 5, "conectividad": 4, "comercios": 4,
        //         "espaciosVerdes": 3 }, "texto": "...", "aniosEnZona": 9, "temas": ["transporte"] }
        // Creates your review of the zone or replaces it (one per user and zone)
        [Authorize]
        [HttpPut("mia")]
        [ProducesResponseType<ResenaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GuardarMia(string slug, GuardarResenaDto request, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            return Ok(await _resenaService.GuardarMiaAsync(slug, sesion, request, cancellationToken));
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

            await _resenaService.EliminarMiaAsync(slug, usuarioId, cancellationToken);
            return NoContent();
        }

        // PUT /Zonas/villa-luro/resenas/{id}/util     marks a review of another user as useful
        // DELETE /Zonas/villa-luro/resenas/{id}/util  removes the mark (both are idempotent)
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

            await _resenaService.MarcarUtilAsync(slug, id, usuarioId, util, cancellationToken);
            return NoContent();
        }
    }
}
