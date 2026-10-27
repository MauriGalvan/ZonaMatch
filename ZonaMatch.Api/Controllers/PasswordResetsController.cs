using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZonaMatch.Application.DTOs.PasswordResets.Requests;
using ZonaMatch.Application.UseCases.PasswordResets;

namespace ZonaMatch.Api.Controllers;

// Password recovery: an email with a one-time link, and the new password chosen with that link
[ApiController]
[Route("api/v1/password-resets")]
[EnableRateLimiting("password-reset")]
public sealed class PasswordResetsController : ControllerBase
{
    // POST /api/v1/password-resets   Body: { "email": "..." }
    // Always 204, whether the email has an account or not
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create(
        [FromBody] RequestPasswordResetRequest request,
        [FromServices] RequestPasswordReset requestPasswordReset,
        CancellationToken cancellationToken)
    {
        await requestPasswordReset.ExecuteAsync(request, cancellationToken);
        return NoContent();
    }

    // POST /api/v1/password-resets/confirmation   Body: { "token": "...", "password": "...", "confirmPassword": "..." }
    [HttpPost("confirmation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Confirm(
        [FromBody] CompletePasswordResetRequest request,
        [FromServices] CompletePasswordReset completePasswordReset,
        CancellationToken cancellationToken)
    {
        await completePasswordReset.ExecuteAsync(request, cancellationToken);
        return NoContent();
    }
}
