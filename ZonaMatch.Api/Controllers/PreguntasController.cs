using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Controllers
{
    // Questions to the neighbors of a zone and their answers. Reading is public; writing needs a logged-in user.
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/preguntas")]
    public class PreguntasController : ControllerBase
    {
        private readonly IPreguntaService _preguntaService;

        public PreguntasController(IPreguntaService preguntaService)
        {
            _preguntaService = preguntaService;
        }

        // GET /Zonas/villa-luro/preguntas
        // Newest question first, each with its answers (oldest first)
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<PreguntaDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(string slug, CancellationToken cancellationToken) =>
            Ok(await _preguntaService.ListarAsync(slug, User.UsuarioId(), cancellationToken));

        // POST /Zonas/villa-luro/preguntas    Body: { "texto": "..." }
        [Authorize]
        [HttpPost]
        [ProducesResponseType<PreguntaDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Crear(string slug, CrearPreguntaDto request, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var pregunta = await _preguntaService.CrearAsync(slug, sesion, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, pregunta);
        }

        // POST /Zonas/villa-luro/preguntas/{id}/respuestas    Body: { "texto": "..." }
        [Authorize]
        [HttpPost("{id:guid}/respuestas")]
        [ProducesResponseType<RespuestaDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Responder(string slug, Guid id, CrearRespuestaDto request, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var respuesta = await _preguntaService.ResponderAsync(slug, id, sesion, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, respuesta);
        }
    }
}
