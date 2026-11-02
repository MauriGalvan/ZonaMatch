using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Scoring;

namespace ZonaMatch.Api.Controllers
{
    // Public endpoint: guests can also calculate a ranking by including their criteria in the request.
    [ApiController]
    [Route("api/v1/scores")]
    public class ScoresController : ControllerBase
    {
        // Above this the request is logged as a warning so slow scoring (usually MOTIS) is easy to spot
        private const long SlowScoringMilliseconds = 5_000;

        // POST /api/v1/scores
        // Body: { "initialZone": "villa-luro", "addedZones": [], "criteria": { "seguridad": "alta" },
        //         "places": [{ "name": "Trabajo", "latitude": -34.6, "longitude": -58.38, "priority": "Alta", "maximumTravelTime": "45 min" }],
        //         "maximumRent": "800000", "mobility": ["transporte-publico"] }
        [HttpPost]
        [ProducesResponseType<ScoresResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CalculateAsync(
            [FromBody] CalculateScoresRequest request,
            [FromServices] CalculateScores useCase,
            [FromServices] ILogger<ScoresController> logger,
            CancellationToken cancellationToken)
        {
            // Guests can score fewer places than signed-in users.
            if (User.GetAuthenticatedUserId() is null && request.Places.Count > ScoringPlaceLimits.Guest)
                throw new BadRequestException(ScoringPlaceLimits.GuestMessage);

            // Saved analyses go straight to the use case, so older ones without limits still recalculate
            MandatoryScoringLimits.Validate(
                request.MaximumRent, request.Operation, request.MaximumPurchasePriceUsd, request.Places.Select(place => place.MaximumTravelTime));

            var stopwatch = Stopwatch.StartNew();
            var result = await useCase.ExecuteAsync(request, cancellationToken);
            stopwatch.Stop();

            logger.Log(
                stopwatch.ElapsedMilliseconds > SlowScoringMilliseconds ? LogLevel.Warning : LogLevel.Information,
                "Scores calculated in {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);

            return Ok(result);
        }
    }
}
