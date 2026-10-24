using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.DTOs.Notifications.Responses
{
    public record NotificationResponse(
        Guid Id,
        NotificationType Type,
        string Title,
        string Message,
        string Link,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReadAt);
}
