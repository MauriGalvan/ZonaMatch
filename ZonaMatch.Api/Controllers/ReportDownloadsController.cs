using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.UseCases.ReportDownload;

namespace ZonaMatch.Api.Controllers;

// Public: the analysis result page reports each PDF download, for the admin statistics. Visitors count too.
[ApiController]
[Route("api/v1/report-downloads")]
public sealed class ReportDownloadsController : ControllerBase
{
    // POST /api/v1/report-downloads (no body)
    [HttpPost]
    [EnableRateLimiting("report-download")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateAsync([FromServices] RegisterReportDownload useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(User.GetAuthenticatedUserId(), cancellationToken);
        return NoContent();
    }
}
