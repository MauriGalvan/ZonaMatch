using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class AnalisisController : ControllerBase
    {
        private readonly IAnalisisService _analisisService;

        public AnalisisController(IAnalisisService analisisService)
        {
            _analisisService = analisisService;
        }

        // GET /Analisis — list the current user's saved analyses
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<AnalisisDetalladoDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ObtenerTodos(CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioId();
            var analisis = await _analisisService.ObtenerTodosAsync(usuarioId, cancellationToken);
            return Ok(analisis);
        }

        // GET /Analisis/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType<AnalisisDetalladoDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerPorId(Guid id, CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioId();
            var analisis = await _analisisService.ObtenerPorIdAsync(id, usuarioId, cancellationToken);
            return Ok(analisis);
        }

        // POST /Analisis
        // Body: { "nombre": "", "contexto": {}, "criterios": {}, "puntos": {} }
        [HttpPost]
        [ProducesResponseType<AnalisisDetalladoDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Crear(CrearAnalisisRequest request, CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioId();
            var analisis = await _analisisService.CrearAsync(usuarioId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, analisis);
        }

        // PUT /Analisis/{id} — update name and/or any of the payload blocks
        [HttpPut("{id:guid}")]
        [ProducesResponseType<AnalisisDetalladoDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Actualizar(Guid id, ActualizarAnalisisRequest request, CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioId();
            var analisis = await _analisisService.ActualizarAsync(id, usuarioId, request, cancellationToken);
            return Ok(analisis);
        }

        // DELETE /Analisis/{id}
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken)
        {
            var usuarioId = ObtenerUsuarioId();
            await _analisisService.EliminarAsync(id, usuarioId, cancellationToken);
            return NoContent();
        }

        // The JWT always carries "sub"; [Authorize] already rejected anonymous requests.
        private Guid ObtenerUsuarioId()
        {
            var id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(id, out var usuarioId))
                throw new UnauthorizedAccessException();

            return usuarioId;
        }
    }
}
