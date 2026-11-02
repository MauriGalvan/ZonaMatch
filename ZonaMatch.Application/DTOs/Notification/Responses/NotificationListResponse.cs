namespace ZonaMatch.Application.DTOs.Notification.Responses
{
    public record NotificationListResponse(IReadOnlyList<NotificationResponse> Items, int UnreadCount);
}
