using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.UseCases.Investor;

namespace ZonaMatch.Api.Controllers;

// Investor profile of the signed-in user and its zone ranking. Every action needs the access an administrator grants
// (403 otherwise).
[ApiController]
[Route("api/v1/investor-profile")]
[Authorize]
public sealed class InvestorProfileController : ControllerBase
{
    // GET /api/v1/investor-profile
    [HttpGet]
    [ProducesResponseType<InvestorProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InvestorProfileResponse>> GetAsync(
        [FromServices] GetMyInvestorProfile useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(User.RequireUserId(), cancellationToken));

    // PUT /api/v1/investor-profile
    [HttpPut]
    [ProducesResponseType<InvestorProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InvestorProfileResponse>> SaveAsync(
        [FromBody] InvestorProfileRequest request,
        [FromServices] SaveMyInvestorProfile useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(User.RequireUserId(), request, cancellationToken));

    // POST /api/v1/investor-profile/ranking    Zones ranked for the saved investor profile.
    [HttpPost("ranking")]
    [ProducesResponseType<InvestmentRankingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InvestmentRankingResponse>> RankAsync(
        [FromServices] CalculateInvestmentRanking useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(User.RequireUserId(), cancellationToken));
}
