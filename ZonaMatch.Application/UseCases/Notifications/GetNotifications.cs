using ZonaMatch.Application.DTOs.Notifications.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Notifications
{
    public class GetNotifications
    {
        public const int MaxItems = 30;

        private readonly INotificationRepository _notifications;

        public GetNotifications(INotificationRepository notifications)
        {
            _notifications = notifications;
        }

        public async Task<NotificationListResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var items = await _notifications.GetForUserAsync(userId, MaxItems, cancellationToken);
            var unreadCount = await _notifications.GetUnreadCountAsync(userId, cancellationToken);
            return new NotificationListResponse(items.Select(Map).ToList(), unreadCount);
        }

        private static NotificationResponse Map(Domain.Entities.UserNotification notification) => new(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Link,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
