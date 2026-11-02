using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZonaMatch.Application.DTOs.Contact.Requests;
using ZonaMatch.Application.UseCases.Contact;

namespace ZonaMatch.Api.Controllers;

// Public landing-page contact endpoint; each valid submission emails the team.
[ApiController]
[Route("api/v1/contact-messages")]
public sealed class ContactMessagesController : ControllerBase
{
    // POST /api/v1/contact-messages Body: { "email": "...", "name": "...", "zone": "...", "website": "" }
    [HttpPost]
    [EnableRateLimiting("contact")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] SendContactMessageRequest request,
        [FromServices] SendContactMessage useCase,
        CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(request, cancellationToken);
        return NoContent();
    }
}
