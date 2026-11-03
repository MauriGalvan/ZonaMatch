using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Notification;

namespace ZonaMatch.Tests.UseCases.Notification;

public class MarkAllNotificationsAsReadTests
{
    [Fact]
    public async Task MarkAllNotificationsAsRead_UsesCurrentTimeAndUserId()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 20, 12, 30, 0, TimeSpan.Zero);
        var repository = new Mock<INotificationRepository>();
        var timeProvider = new FixedTimeProvider(now);

        await new MarkAllNotificationsAsRead(repository.Object, timeProvider).ExecuteAsync(userId);

        repository.Verify(item => item.MarkAllAsReadAsync(userId, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
