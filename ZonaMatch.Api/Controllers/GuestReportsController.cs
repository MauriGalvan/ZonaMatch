using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZonaMatch.Application.DTOs.GuestReport.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.GuestReport;

namespace ZonaMatch.Api.Controllers;

// Public: a visitor without an account receives the PDF report of their analysis by email
[ApiController]
[Route("api/v1/guest-reports")]
public sealed class GuestReportsController : ControllerBase
{
    // POST /api/v1/guest-reports (multipart/form-data): email, website (hidden) and report (the PDF)
    [HttpPost]
    [EnableRateLimiting("guest-report")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SendGuestReport.MaxReportBytes + 64 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendAsync(
        [FromForm] SendGuestReportRequest request,
        IFormFile? report,
        [FromServices] SendGuestReport sendGuestReport,
        CancellationToken cancellationToken)
    {
        byte[]? content = null;
        if (report is { Length: > 0 and <= SendGuestReport.MaxReportBytes })
        {
            using var stream = new MemoryStream((int)report.Length);
            await report.CopyToAsync(stream, cancellationToken);
            content = stream.ToArray();
        }

        await sendGuestReport.ExecuteAsync(request, content, cancellationToken);
        return NoContent();
    }
}
