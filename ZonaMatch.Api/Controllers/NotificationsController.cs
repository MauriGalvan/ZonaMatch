using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Notifications.Responses;
using ZonaMatch.Application.UseCases.Notifications;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<NotificationListResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<NotificationListResponse>> GetAsync(
            [FromServices] GetNotifications useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            return Ok(await useCase.ExecuteAsync(userId, cancellationToken));
        }

        [HttpPatch("{id:guid}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkAsReadAsync(
            [FromRoute] Guid id,
            [FromServices] MarkNotificationAsRead useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await useCase.ExecuteAsync(id, userId, cancellationToken);
            return NoContent();
        }

        [HttpPatch("read-all")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> MarkAllAsReadAsync(
            [FromServices] MarkAllNotificationsAsRead useCase,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            await useCase.ExecuteAsync(userId, cancellationToken);
            return NoContent();
        }
    }
}
