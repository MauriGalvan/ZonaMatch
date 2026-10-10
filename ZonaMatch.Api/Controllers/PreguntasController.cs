using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Preguntas;

namespace ZonaMatch.Api.Controllers
{
    // Preguntas a los vecinos de una zona y sus respuestas. Leer es publico; escribir pide un usuario con sesion iniciada.
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/preguntas")]
    public class PreguntasController : ControllerBase
    {
        private readonly ListarPreguntas _listarPreguntas;
        private readonly CrearPregunta _crearPregunta;
        private readonly ResponderPregunta _responderPregunta;

        public PreguntasController(
            ListarPreguntas listarPreguntas,
            CrearPregunta crearPregunta,
            ResponderPregunta responderPregunta)
        {
            _listarPreguntas = listarPreguntas;
            _crearPregunta = crearPregunta;
            _responderPregunta = responderPregunta;
        }

        // GET /Zonas/villa-luro/preguntas
        // La pregunta mas nueva primero, cada una con sus respuestas (la mas vieja primero)
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<PreguntaDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(string slug, CancellationToken cancellationToken) =>
            Ok(await _listarPreguntas.EjecutarAsync(slug, User.UsuarioId(), cancellationToken));

        // POST /Zonas/villa-luro/preguntas    Cuerpo: { "texto": "..." }
        [Authorize]
        [HttpPost]
        [ProducesResponseType<PreguntaDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Crear(string slug, CrearPreguntaDto solicitud, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var pregunta = await _crearPregunta.EjecutarAsync(slug, sesion, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, pregunta);
        }

        // POST /Zonas/villa-luro/preguntas/{id}/respuestas    Cuerpo: { "texto": "..." }
        [Authorize]
        [HttpPost("{id:guid}/respuestas")]
        [ProducesResponseType<RespuestaDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Responder(string slug, Guid id, CrearRespuestaDto solicitud, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var respuesta = await _responderPregunta.EjecutarAsync(slug, id, sesion, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, respuesta);
        }
    }
}
