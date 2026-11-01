using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.UseCases.SavedAnalyses;

namespace ZonaMatch.Api.Controllers
{
    // Saved user analyses include compared zones, a criteria snapshot, and adjusted versions.
    [ApiController]
    // Keep the legacy route explicit while controller and action names are migrated.
    [Route("Analisis")]
    [Authorize]
    public class AnalysesController : ControllerBase
    {
        // GET /Analisis returns original analyses with their versions, newest first.
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<AnalysisResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMineAsync(
            [FromServices] ListMyAnalyses listMyAnalyses,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            return Ok(await listMyAnalyses.ExecuteAsync(userId, cancellationToken));
        }

        // Keep the original request and response contract available to existing clients.
        [HttpPost("Comparar")]
        [ProducesResponseType<LegacyAnalysisComparisonResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompareLegacyAsync(
            [FromBody] LegacyCompareAnalysesRequest legacyRequest,
            [FromServices] CompareAnalyses compareAnalyses,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var response = await compareAnalyses.ExecuteAsync(
                userId,
                new CompareAnalysesRequest { AnalysisIds = legacyRequest.AnalysisIds },
                cancellationToken);
            return Ok(ToLegacyResponse(response));
        }

        // POST /api/v1/analyses/comparisons
        [HttpPost("~/api/v1/analyses/comparisons")]
        [ProducesResponseType<AnalysisComparisonResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompareAsync(
            [FromBody] CompareAnalysesRequest request,
            [FromServices] CompareAnalyses compareAnalyses,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            return Ok(await compareAnalyses.ExecuteAsync(userId, request, cancellationToken));
        }

        // GET /Analisis/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByIdAsync(
            [FromRoute] Guid id,
            [FromServices] GetAnalysisById getAnalysis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            return Ok(await getAnalysis.ExecuteAsync(id, userId, cancellationToken));
        }

        // POST /Analisis
        // Body: { "nombre": "Cerca del trabajo", "zonas": [{ "slug": "villa-luro", "nombre": "Villa Luro, CABA", "esInicial": true }] }
        // Criteria are copied from the profile (404 if the user has not created one yet).
        [HttpPost]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateAsync(
            [FromBody] CreateAnalysisRequest request,
            [FromServices] CreateAnalysis createAnalysis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var analysis = await createAnalysis.ExecuteAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = analysis.Id }, analysis);
        }

        // POST /Analisis/{id}/versiones
        // Body: { "nombre": null, "criterio": { "contexto": {}, "criterios": {}, "puntos": {} }, "zonas": null }
        // Saves adjusted criteria as a version of the original, even when {id} belongs to another version.
        [HttpPost("{id:guid}/versiones")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateVersionAsync(
            [FromRoute] Guid id,
            [FromBody] CreateAnalysisVersionRequest request,
            [FromServices] CreateAnalysisVersion createAnalysisVersion,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var version = await createAnalysisVersion.ExecuteAsync(id, userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = version.Id }, version);
        }

        // POST /api/v1/analyses/{id}/recalculations
        // Recalculates the analysis with today's data as a new version (same criteria and zones, the profile
        // untouched); the earlier result is kept, so both can be compared.
        [HttpPost("~/api/v1/analyses/{id:guid}/recalculations")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecalculateAsync(
            [FromRoute] Guid id,
            [FromServices] RecalculateAnalysis useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var version = await useCase.ExecuteAsync(id, userId, cancellationToken);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = version.Id }, version);
        }

        // PUT /api/v1/analyses/{id}/favorite    Body: { "isFavorite": true }
        // Stars or unstars an analysis or a version in the user's saved analyses.
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
        public async Task<IActionResult> RenameAsync(
            [FromRoute] Guid id,
            [FromBody] RenameAnalysisRequest request,
            [FromServices] RenameAnalysis renameAnalysis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            return Ok(await renameAnalysis.ExecuteAsync(id, userId, request, cancellationToken));
        }

        // DELETE /Analisis/{id} deletes an original and all of its versions.
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(
            [FromRoute] Guid id,
            [FromServices] DeleteAnalysis deleteAnalysis,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await deleteAnalysis.ExecuteAsync(id, userId, cancellationToken);
            return NoContent();
        }

        private static LegacyAnalysisComparisonResponse ToLegacyResponse(AnalysisComparisonResponse response) =>
            new(response.Analyses
                .Select(analysis => new LegacyComparedAnalysisResponse(
                    analysis.Id,
                    analysis.Name,
                    analysis.TravelMode,
                    analysis.Zones.Select(zone => new PuntajeZonaDto(
                        zone.Slug,
                        zone.Name,
                        zone.Score,
                        zone.Components.Select(component => new ComponentePuntajeDto(
                            component.Criterion, component.Priority, component.Score, component.Details)).ToArray(),
                        zone.Limits.Select(limit => new LimitePuntajeDto(
                            limit.Type, limit.IsMet, limit.Details)).ToArray(),
                        zone.Places.Select(place => new TiempoLugarDto(
                            place.Name, place.Minutes, place.MaximumMinutes, place.IsEstimated, place.DistanceKm)).ToArray(),
                        zone.Rent is { } rent
                            ? new AlquilerZonaDto(
                                rent.Average, rent.AvailableProperties, rent.Expenses, rent.MonthlyCost,
                                rent.PricePerM2, rent.SurfaceM2, rent.Estimated)
                            : null,
                        zone.Sale is { } sale
                            ? new CompraZonaDto(
                                sale.AverageUsd, sale.AvailableProperties, sale.Expenses, sale.PricePerM2Usd,
                                sale.SurfaceM2, sale.EstimatedUsd)
                            : null)).ToArray(),
                    analysis.GroupScores))
                .ToArray());
    }
}
