using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.UseCases.AnalisisGuardados;

namespace ZonaMatch.Api.Controllers
{
    // Saved user analyses: compared zones, a snapshot of the user's criteria, and adjusted versions.
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class AnalisisController : ControllerBase
    {
        // GET /Analisis returns original analyses with their versions, newest first.
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<AnalysisResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Listar(
            [FromServices] ListarMisAnalisis listarMisAnalisis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            return Ok(await listarMisAnalisis.EjecutarAsync(usuarioId, cancellationToken));
        }

        // POST /Analisis/Comparar recalculates saved zones using each selected analysis.
        [HttpPost("Comparar")]
        [ProducesResponseType<ComparacionAnalisisDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Comparar(
            [FromBody] CompararAnalisisRequest solicitud,
            [FromServices] CompararAnalisis compararAnalisis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            return Ok(await compararAnalisis.EjecutarAsync(usuarioId, solicitud, cancellationToken));
        }

        // GET /Analisis/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Obtener(
            Guid id,
            [FromServices] ObtenerAnalisis obtenerAnalisis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            return Ok(await obtenerAnalisis.EjecutarAsync(id, usuarioId, cancellationToken));
        }

        // POST /Analisis
        // Body: { "nombre": "Cerca del trabajo", "zonas": [{ "slug": "villa-luro", "nombre": "Villa Luro, CABA", "esInicial": true }] }
        // Criteria are copied from the profile (404 if the user has not created one yet).
        [HttpPost]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Crear(
            [FromBody] CreateAnalysisRequest solicitud,
            [FromServices] CrearAnalisis crearAnalisis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            var analisis = await crearAnalisis.EjecutarAsync(usuarioId, solicitud, cancellationToken);
            return CreatedAtAction(nameof(Obtener), new { id = analisis.Id }, analisis);
        }

        // POST /Analisis/{id}/versiones
        // Body: { "nombre": null, "criterio": { "contexto": {}, "criterios": {}, "puntos": {} }, "zonas": null }
        // Saves adjusted criteria as a version of the original, even when {id} belongs to another version.
        [HttpPost("{id:guid}/versiones")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CrearVersion(
            Guid id,
            [FromBody] CreateAnalysisVersionRequest solicitud,
            [FromServices] CrearVersionAnalisis crearVersionAnalisis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            var version = await crearVersionAnalisis.EjecutarAsync(id, usuarioId, solicitud, cancellationToken);
            return CreatedAtAction(nameof(Obtener), new { id = version.Id }, version);
        }

        // POST /api/v1/analyses/{id}/recalculations
        // Recalculates the analysis with today's data as a new version (same criteria and zones, the profile
        // untouched); the earlier result is kept, so both can be compared.
        [HttpPost("~/api/v1/analyses/{id:guid}/recalculations")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Recalculate(
            [FromRoute] Guid id,
            [FromServices] RecalculateAnalysis useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var version = await useCase.ExecuteAsync(id, userId, cancellationToken);
            return CreatedAtAction(nameof(Obtener), new { id = version.Id }, version);
        }

        // PUT /api/v1/analyses/{id}/favorite    Body: { "isFavorite": true }
        // Stars or unstars an analysis or a version in "Mis análisis".
        [HttpPut("~/api/v1/analyses/{id:guid}/favorite")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetFavorite(
            [FromRoute] Guid id,
            [FromBody] SetAnalysisFavoriteRequest request,
            [FromServices] SetAnalysisFavorite useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await useCase.ExecuteAsync(id, userId, request.IsFavorite!.Value, cancellationToken);
            return NoContent();
        }

        // PUT /Analisis/{id} — Body: { "nombre": "..." }
        [HttpPut("{id:guid}")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Renombrar(
            Guid id,
            [FromBody] RenameAnalysisRequest solicitud,
            [FromServices] RenombrarAnalisis renombrarAnalisis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            return Ok(await renombrarAnalisis.EjecutarAsync(id, usuarioId, solicitud, cancellationToken));
        }

        // DELETE /Analisis/{id} deletes an original and all of its versions.
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Eliminar(
            Guid id,
            [FromServices] EliminarAnalisis eliminarAnalisis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } usuarioId)
                return Unauthorized();

            await eliminarAnalisis.EjecutarAsync(id, usuarioId, cancellationToken);
            return NoContent();
        }
    }
}
