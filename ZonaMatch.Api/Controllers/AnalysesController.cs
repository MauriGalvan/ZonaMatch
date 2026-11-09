using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.UseCases.SavedAnalysis;

namespace ZonaMatch.Api.Controllers
{
    // Saved user analyses include compared zones, a criteria snapshot, and adjusted versions.
    [ApiController]
    [Route("api/v1/analyses")]
    [Authorize]
    public class AnalysesController : ControllerBase
    {
        // GET /api/v1/analyses returns original analyses with their versions, newest first.
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<AnalysisResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMineAsync(
            [FromServices] ListMyAnalyses listMyAnalyses,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            return Ok(await listMyAnalyses.ExecuteAsync(userId, cancellationToken));
        }

        // Keep the original request and response contract available to existing clients.
        [HttpPost("legacy-comparisons")]
        [ProducesResponseType<LegacyAnalysisComparisonResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompareLegacyAsync(
            [FromBody] LegacyCompareAnalysesRequest legacyRequest,
            [FromServices] CompareAnalyses compareAnalyses,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            var response = await compareAnalyses.ExecuteAsync(
                userId,
                new CompareAnalysesRequest { AnalysisIds = legacyRequest.AnalysisIds },
                cancellationToken);
            return Ok(ToLegacyResponse(response));
        }

        // POST /api/v1/analyses/comparisons
        [HttpPost("comparisons")]
        [ProducesResponseType<AnalysisComparisonResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompareAsync(
            [FromBody] CompareAnalysesRequest request,
            [FromServices] CompareAnalyses compareAnalyses,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            return Ok(await compareAnalyses.ExecuteAsync(userId, request, cancellationToken));
        }

        // GET /api/v1/analyses/{id}
        [HttpGet("{id:guid}")]
        [ActionName(nameof(GetByIdAsync))]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByIdAsync(
            [FromRoute] Guid id,
            [FromServices] GetAnalysisById getAnalysis,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            return Ok(await getAnalysis.ExecuteAsync(id, userId, cancellationToken));
        }

        // POST /api/v1/analyses
        // Body: { "name": "Near work", "zones": [{ "slug": "villa-luro", "name": "Villa Luro, CABA", "isInitial": true }] }
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
            var userId = User.RequireUserId();
            var analysis = await createAnalysis.ExecuteAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = analysis.Id }, analysis);
        }

        // POST /api/v1/analyses/{id}/versions
        // Body: { "name": null, "criterion": { "context": {}, "criteria": {}, "points": {} }, "zones": null }
        // Saves adjusted criteria as a version of the original, even when {id} belongs to another version.
        [HttpPost("{id:guid}/versions")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateVersionAsync(
            [FromRoute] Guid id,
            [FromBody] CreateAnalysisVersionRequest request,
            [FromServices] CreateAnalysisVersion createAnalysisVersion,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            var version = await createAnalysisVersion.ExecuteAsync(id, userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = version.Id }, version);
        }

        // POST /api/v1/analyses/{id}/recalculations
        // Recalculates the analysis with today's data as a new version (same criteria and zones, the profile
        // untouched); the earlier result is kept, so both can be compared.
        [HttpPost("{id:guid}/recalculations")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecalculateAsync(
            [FromRoute] Guid id,
            [FromServices] RecalculateAnalysis useCase,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            var version = await useCase.ExecuteAsync(id, userId, cancellationToken);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = version.Id }, version);
        }

        // PUT /api/v1/analyses/{id}/favorite    Body: { "isFavorite": true }
        // Stars or unstars an analysis or a version in the user's saved analyses.
        [HttpPut("{id:guid}/favorite")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetFavoriteAsync(
            [FromRoute] Guid id,
            [FromBody] SetAnalysisFavoriteRequest request,
            [FromServices] SetAnalysisFavorite useCase,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            await useCase.ExecuteAsync(id, userId, request.IsFavorite!.Value, cancellationToken);
            return NoContent();
        }

        // PUT /api/v1/analyses/{id} — Body: { "name": "..." }
        [HttpPut("{id:guid}")]
        [ProducesResponseType<AnalysisResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RenameAsync(
            [FromRoute] Guid id,
            [FromBody] RenameAnalysisRequest request,
            [FromServices] RenameAnalysis renameAnalysis,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            return Ok(await renameAnalysis.ExecuteAsync(id, userId, request, cancellationToken));
        }

        // GET /api/v1/analyses/trash
        [HttpGet("trash")]
        [ProducesResponseType<IReadOnlyList<TrashedAnalysisResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTrashAsync(
            [FromServices] ListAnalysisTrash listAnalysisTrash,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            return Ok(await listAnalysisTrash.ExecuteAsync(userId, cancellationToken));
        }

        // POST /api/v1/analyses/trash/{id}/restore
        [HttpPost("trash/{id:guid}/restore")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RestoreAsync(
            [FromRoute] Guid id,
            [FromServices] RestoreAnalysis restoreAnalysis,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            await restoreAnalysis.ExecuteAsync(id, userId, cancellationToken);
            return NoContent();
        }

        // DELETE /api/v1/analyses/trash/{id} permanently deletes one trashed analysis.
        [HttpDelete("trash/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PermanentlyDeleteAsync(
            [FromRoute] Guid id,
            [FromServices] PermanentlyDeleteAnalysis permanentlyDeleteAnalysis,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            await permanentlyDeleteAnalysis.ExecuteAsync(id, userId, cancellationToken);
            return NoContent();
        }

        // DELETE /api/v1/analyses/trash permanently deletes all of the user's trashed analyses.
        [HttpDelete("trash")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> EmptyTrashAsync(
            [FromServices] EmptyAnalysisTrash emptyAnalysisTrash,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            await emptyAnalysisTrash.ExecuteAsync(userId, cancellationToken);
            return NoContent();
        }

        // DELETE /api/v1/analyses/{id} moves an analysis to the trash.
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(
            [FromRoute] Guid id,
            [FromServices] DeleteAnalysis deleteAnalysis,
            CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            await deleteAnalysis.ExecuteAsync(id, userId, cancellationToken);
            return NoContent();
        }

        private static LegacyAnalysisComparisonResponse ToLegacyResponse(AnalysisComparisonResponse response) =>
            new(response.Analyses
                .Select(analysis => new LegacyComparedAnalysisResponse(
                    analysis.Id,
                    analysis.Name,
                    analysis.TravelMode,
                    analysis.Zones.Select(zone => new LegacyScoredZoneResponse(
                        zone.Slug,
                        zone.Name,
                        zone.Score,
                        zone.Components.Select(component => new LegacyScoreComponentResponse(
                            component.Criterion, component.Priority, component.Score, component.Details)).ToArray(),
                        zone.Limits.Select(limit => new LegacyScoreLimitResponse(
                            limit.Type, limit.IsMet, limit.Details)).ToArray(),
                        zone.Places.Select(place => new LegacyPlaceTravelTimeResponse(
                            place.Name, place.Minutes, place.MaximumMinutes, place.IsEstimated, place.DistanceKm)).ToArray(),
                        zone.Rent is { } rent
                            ? new LegacyRentResponse(
                                rent.Average, rent.AvailableProperties, rent.Expenses, rent.MonthlyCost,
                                rent.PricePerM2, rent.SurfaceM2, rent.Estimated)
                            : null,
                        zone.Sale is { } sale
                            ? new LegacySaleResponse(
                                sale.AverageUsd, sale.AvailableProperties, sale.Expenses, sale.PricePerM2Usd,
                                sale.SurfaceM2, sale.EstimatedUsd)
                            : null)).ToArray(),
                    analysis.GroupScores))
                .ToArray());
    }
}
