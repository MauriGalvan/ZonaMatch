using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZonaMatch.Application.DTOs.GroupInvitation;
using ZonaMatch.Application.DTOs.GroupLink;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Application.UseCases.GroupLinks;

namespace ZonaMatch.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class InvitationsController : ControllerBase
{
    private Guid GetCurrentUserId()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdString!);
    }

    // ==========================================
    // INVITACIONES POR CORREO ELECTRÓNICO
    // ==========================================

    [Authorize]
    [HttpPost("Email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateEmailInvitation(
        [FromBody] CreateInvitationRequest request,
        [FromServices] CreateInvitation createInvitation,
        [FromServices] IConfiguration configuration,
        [FromServices] IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var frontendBaseUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            var requestOrigin = Request.Headers["Origin"].ToString().TrimEnd('/');
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            frontendBaseUrl = allowedOrigins.FirstOrDefault(origin =>
                string.Equals(origin.TrimEnd('/'), requestOrigin, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(frontendBaseUrl) && environment.IsDevelopment())
            frontendBaseUrl = "http://localhost:5173";

        // Deployment mistake, not a client error: the global exception handler logs it and answers 500
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new InvalidOperationException("Frontend:BaseUrl is not configured; cannot build the invitation link.");

        await createInvitation.ExecuteAsync(request, GetCurrentUserId(), cancellationToken, frontendBaseUrl);
        return Ok(new { Message = "Invitación enviada por correo exitosamente." });
    }

    [HttpGet("Email/{token:guid}")]
    [ProducesResponseType(typeof(InvitationDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmailInvitationDetails(
        [FromRoute] Guid token,
        [FromServices] GetInvitationByToken getInvitation,
        CancellationToken cancellationToken)
    {
        var response = await getInvitation.ExecuteAsync(token, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("Email/{token:guid}/Accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptEmailInvitation(
        [FromRoute] Guid token,
        [FromServices] AcceptInvitation acceptInvitation,
        CancellationToken cancellationToken)
    {
        await acceptInvitation.ExecuteAsync(token, GetCurrentUserId(), cancellationToken);
        return Ok(new { Message = "¡Te has unido al grupo exitosamente!" });
    }

    // ==========================================
    // INVITACIONES POR LINK PÚBLICO (ESTILO WHATSAPP)
    // ==========================================

    [Authorize]
    [HttpGet("Link/Group/{groupId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentGroupInviteLink(
        [FromRoute] Guid groupId,
        [FromServices] GetGroupInviteLink getGroupInviteLink,
        CancellationToken cancellationToken)
    {
        var token = await getGroupInviteLink.ExecuteAsync(groupId, GetCurrentUserId(), cancellationToken);
        return Ok(new { InviteToken = token });
    }

    [HttpGet("Link/{token:guid}")]
    [ProducesResponseType(typeof(GroupLinkDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLinkInvitationDetails(
        [FromRoute] Guid token,
        [FromServices] GetGroupByLink getGroupByLink,
        CancellationToken cancellationToken)
    {
        var response = await getGroupByLink.ExecuteAsync(token, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("Link/{token:guid}/Accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> JoinByLink(
        [FromRoute] Guid token,
        [FromServices] JoinGroupByLink joinGroupByLink,
        CancellationToken cancellationToken)
    {
        await joinGroupByLink.ExecuteAsync(token, GetCurrentUserId(), cancellationToken);
        return Ok(new { Message = "¡Te has unido al grupo exitosamente!" });
    }

    [Authorize]
    [HttpPut("Link/Group/{groupId:guid}/Revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeLink(
        [FromRoute] Guid groupId,
        [FromServices] RevokeInviteLink revokeInviteLink,
        CancellationToken cancellationToken)
    {
        var newToken = await revokeInviteLink.ExecuteAsync(groupId, GetCurrentUserId(), cancellationToken);
        return Ok(new { InviteToken = newToken, Message = "Enlace revocado exitosamente." });
    }
}
