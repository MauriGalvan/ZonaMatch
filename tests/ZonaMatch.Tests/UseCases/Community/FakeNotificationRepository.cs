using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Community;

internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<UserNotification> Notifications { get; } = [];

    public Task<IReadOnlyList<UserNotification>> GetForUserAsync(
        Guid userId, int limit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<UserNotification?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void AddRange(IEnumerable<UserNotification> notifications) => Notifications.AddRange(notifications);

    public Task MarkAllAsReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public List<Guid> ReadModerationReviewIds { get; } = [];

    public Task MarkReviewModerationAsReadAsync(Guid reviewId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        ReadModerationReviewIds.Add(reviewId);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
