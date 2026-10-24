using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Notifications
{
    public class MarkNotificationAsRead
    {
        private readonly INotificationRepository _notifications;
        private readonly TimeProvider _timeProvider;

        public MarkNotificationAsRead(INotificationRepository notifications, TimeProvider timeProvider)
        {
            _notifications = notifications;
            _timeProvider = timeProvider;
        }

        public async Task ExecuteAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
        {
            var notification = await _notifications.GetAsync(notificationId, userId, cancellationToken)
                ?? throw new NotFoundException("Notification not found.");

            notification.ReadAt ??= _timeProvider.GetUtcNow();
            await _notifications.SaveChangesAsync(cancellationToken);
        }
    }
}
