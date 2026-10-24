using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface INotificationRepository
    {
        Task<IReadOnlyList<UserNotification>> GetForUserAsync(
            Guid userId, int limit, CancellationToken cancellationToken = default);

        Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<UserNotification?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

        void AddRange(IEnumerable<UserNotification> notifications);

        Task MarkAllAsReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
