using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Preguntas;

namespace ZonaMatch.Api.Controllers
{
    // Questions for a zone's community and their answers. Reading is public; writing requires an authenticated user.
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/preguntas")]
    public class PreguntasController : ControllerBase
    {
        // GET /Zonas/villa-luro/preguntas
        // Newest question first; answers within each question are oldest first.
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<PreguntaDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(
            string slug,
            [FromServices] ListarPreguntas listarPreguntas,
            CancellationToken cancellationToken) =>
            Ok(await listarPreguntas.EjecutarAsync(slug, User.GetAuthenticatedUserId(), cancellationToken));

        // POST /Zonas/villa-luro/preguntas    Body: { "texto": "..." }
        [Authorize]
        [HttpPost]
        [ProducesResponseType<PreguntaDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Crear(
            string slug,
            [FromBody] CrearPreguntaDto solicitud,
            [FromServices] CrearPregunta crearPregunta,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserContext() is not { } currentUser)
                return Unauthorized();

            var pregunta = await crearPregunta.EjecutarAsync(slug, currentUser, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, pregunta);
        }

        // POST /Zonas/villa-luro/preguntas/{id}/respuestas    Body: { "texto": "..." }
        [Authorize]
        [HttpPost("{id:guid}/respuestas")]
        [ProducesResponseType<RespuestaDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Responder(
            string slug,
            Guid id,
            [FromBody] CrearRespuestaDto solicitud,
            [FromServices] ResponderPregunta responderPregunta,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserContext() is not { } currentUser)
                return Unauthorized();

            var respuesta = await responderPregunta.EjecutarAsync(slug, id, currentUser, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, respuesta);
        }
    }
}
