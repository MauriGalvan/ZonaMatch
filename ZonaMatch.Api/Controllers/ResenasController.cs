using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Resenas;

namespace ZonaMatch.Api.Controllers
{
    // Community reviews for a zone. Reading is public; writing requires an authenticated user (Bearer token).
    [ApiController]
    [Route("api/v1/zones/{slug:regex(" + RutasZona.Slug + ")}/reviews")]
    public class ResenasController : ControllerBase
    {
        // GET /api/v1/zones/villa-luro/reviews
        // With a token, each review indicates whether it is yours (esMia) and whether you marked it useful.
        [HttpGet]
        [ProducesResponseType<ResenasZonaDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(
            string slug,
            [FromServices] ListarResenas listarResenas,
            CancellationToken cancellationToken) =>
            Ok(await listarResenas.EjecutarAsync(slug, User.GetAuthenticatedUserId(), cancellationToken));

        // PUT /api/v1/zones/villa-luro/reviews/mine
        // Body: { "aspectos": { "seguridad": 3, "transporte": 5, "conectividad": 4, "comercios": 4, "espaciosVerdes": 3 },
        //         "texto": "...", "aniosEnZona": 9, "temas": ["transporte"] }
        // Creates or replaces your review for the zone (one per user and zone). The overall score is the average of its aspects.
        [Authorize]
        [HttpPut("mine")]
        [ProducesResponseType<ResenaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GuardarMia(
            string slug,
            [FromBody] GuardarResenaDto solicitud,
            [FromServices] GuardarMiResena guardarMiResena,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserContext() is not { } currentUser)
                return Unauthorized();

            return Ok(await guardarMiResena.EjecutarAsync(slug, currentUser, solicitud, cancellationToken));
        }

        // DELETE /api/v1/zones/villa-luro/reviews/mine
        [Authorize]
        [HttpDelete("mine")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EliminarMia(
            string slug,
            [FromServices] EliminarMiResena eliminarMiResena,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await eliminarMiResena.EjecutarAsync(slug, userId, cancellationToken);
            return NoContent();
        }

        // PUT /api/v1/zones/villa-luro/reviews/{id}/helpful-votes marks another user's review as useful.
        // DELETE /api/v1/zones/villa-luro/reviews/{id}/helpful-votes removes the mark; both operations are idempotent.
        [Authorize]
        [HttpPut("{id:guid}/helpful-votes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public Task<IActionResult> MarcarUtil(
            string slug,
            Guid id,
            [FromServices] MarcarResenaUtil marcarResenaUtil,
            CancellationToken cancellationToken) =>
            CambiarUtilAsync(slug, id, util: true, marcarResenaUtil, cancellationToken);

        [Authorize]
        [HttpDelete("{id:guid}/helpful-votes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> DesmarcarUtil(
            string slug,
            Guid id,
            [FromServices] MarcarResenaUtil marcarResenaUtil,
            CancellationToken cancellationToken) =>
            CambiarUtilAsync(slug, id, util: false, marcarResenaUtil, cancellationToken);

        private async Task<IActionResult> CambiarUtilAsync(
            string slug,
            Guid id,
            bool util,
            MarcarResenaUtil marcarResenaUtil,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await marcarResenaUtil.EjecutarAsync(slug, id, userId, util, cancellationToken);
            return NoContent();
        }
    }
}
