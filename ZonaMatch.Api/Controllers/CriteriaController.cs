using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs.Criterion.Responses;
using ZonaMatch.Application.UseCases.Criterion;

namespace ZonaMatch.Api.Controllers;

// Public catalog of criteria used by the detailed and guest analysis wizards.
[ApiController]
[Route("api/v1/criteria")]
public sealed class CriteriaController : ControllerBase
{
    // GET /api/v1/criteria?perfil=inversor    Without perfil, the household profile criteria.
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CriterionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CriterionResponse>>> List(
        [FromServices] ListCriteria listCriteria,
        [FromQuery(Name = "perfil")] string? profile,
        CancellationToken cancellationToken) =>
        Ok(await listCriteria.ExecuteAsync(profile, cancellationToken));
}
