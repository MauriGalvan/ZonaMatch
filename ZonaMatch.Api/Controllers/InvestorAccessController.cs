using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.UseCases.Investor;

namespace ZonaMatch.Api.Controllers;

// Investor profile access of the signed-in user: it is requested here and an administrator grants it.
[ApiController]
[Route("api/v1/investor-access")]
[Authorize]
public sealed class InvestorAccessController : ControllerBase
{
    // GET /api/v1/investor-access
    [HttpGet]
    [ProducesResponseType<InvestorAccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InvestorAccessResponse>> GetAsync(
        [FromServices] GetInvestorAccess useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(User.RequireUserId(), cancellationToken));

    // POST /api/v1/investor-access    Asks for access; repeating it changes nothing.
    [HttpPost]
    [ProducesResponseType<InvestorAccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InvestorAccessResponse>> RequestAsync(
        [FromServices] RequestInvestorAccess useCase,
        CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(User.RequireUserId(), cancellationToken));
}
