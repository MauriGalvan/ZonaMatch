using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.UseCases.Investor;

namespace ZonaMatch.Api.Controllers;

// Saving investment analyses and their versions. They are read, renamed and deleted with the household analyses
// (api/v1/analyses), where they come marked with the investor profile. Needs investor access (403 otherwise).
[ApiController]
[Route("api/v1/investment-analyses")]
[Authorize]
public sealed class InvestmentAnalysesController : ControllerBase
{
    // POST /api/v1/investment-analyses
    [HttpPost]
    [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisResponse>> CreateAsync(
        [FromBody] SaveInvestmentAnalysisRequest request,
        [FromServices] SaveInvestmentAnalysis useCase,
        CancellationToken cancellationToken)
    {
        var analysis = await useCase.ExecuteAsync(User.RequireUserId(), request, cancellationToken);
        return Created($"/api/v1/analyses/{analysis.Id}", analysis);
    }

    // POST /api/v1/investment-analyses/{id}/versions
    [HttpPost("{id:guid}/versions")]
    [ProducesResponseType<AnalysisResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisResponse>> CreateVersionAsync(
        Guid id,
        [FromBody] SaveInvestmentAnalysisRequest request,
        [FromServices] CreateInvestmentAnalysisVersion useCase,
        CancellationToken cancellationToken)
    {
        var version = await useCase.ExecuteAsync(id, User.RequireUserId(), request, cancellationToken);
        return Created($"/api/v1/analyses/{version.Id}", version);
    }
}
