namespace ZonaMatch.Application.DTOs.Notifications.Responses
{
    public record NotificationListResponse(IReadOnlyList<NotificationResponse> Items, int UnreadCount);
}
