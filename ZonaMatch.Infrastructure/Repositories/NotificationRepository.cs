using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly ZonaMatchDbContext _context;

        public NotificationRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<UserNotification>> GetForUserAsync(
            Guid userId, int limit, CancellationToken cancellationToken = default) =>
            await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == userId)
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
            _context.Notifications.CountAsync(
                notification => notification.UserId == userId && notification.ReadAt == null, cancellationToken);

        public Task<UserNotification?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            _context.Notifications.FirstOrDefaultAsync(
                notification => notification.Id == id && notification.UserId == userId, cancellationToken);

        public void AddRange(IEnumerable<UserNotification> notifications) => _context.Notifications.AddRange(notifications);

        public async Task MarkAllAsReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default) =>
            await _context.Notifications
                .Where(notification => notification.UserId == userId && notification.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, readAt), cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
