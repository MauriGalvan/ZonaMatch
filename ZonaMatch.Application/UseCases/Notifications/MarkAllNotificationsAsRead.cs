using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Notifications
{
    public class MarkAllNotificationsAsRead
    {
        private readonly INotificationRepository _notifications;
        private readonly TimeProvider _timeProvider;

        public MarkAllNotificationsAsRead(INotificationRepository notifications, TimeProvider timeProvider)
        {
            _notifications = notifications;
            _timeProvider = timeProvider;
        }

        public Task ExecuteAsync(Guid userId, CancellationToken cancellationToken = default) =>
            _notifications.MarkAllAsReadAsync(userId, _timeProvider.GetUtcNow(), cancellationToken);
    }
}
