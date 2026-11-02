using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Notification;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Notification;

public class MarkNotificationAsReadTests
{
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 20, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task MarkNotificationAsRead_WhenNotificationExists_SetsReadTimeAndSaves()
    {
        var notificationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var notification = new UserNotification { Id = notificationId, UserId = userId };
        var repository = new Mock<INotificationRepository>();
        repository.Setup(item => item.GetAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        await new MarkNotificationAsRead(repository.Object, new FixedTimeProvider(CurrentTime))
            .ExecuteAsync(notificationId, userId);

        notification.ReadAt.Should().Be(CurrentTime);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkNotificationAsRead_WhenNotificationDoesNotBelongToUser_ThrowsNotFound()
    {
        var repository = new Mock<INotificationRepository>();
        repository.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotification?)null);

        Func<Task> action = () => new MarkNotificationAsRead(repository.Object, new FixedTimeProvider(CurrentTime))
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
