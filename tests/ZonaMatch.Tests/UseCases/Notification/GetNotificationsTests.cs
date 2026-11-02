using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Notification;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.UseCases.Notification;

public class GetNotificationsTests
{
    [Fact]
    public async Task GetNotifications_ReturnsMappedItemsAndUnreadCountForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var notification = new UserNotification
        {
            UserId = userId,
            Type = NotificationType.GroupInvitation,
            Title = "Invitation",
            Message = "Join the group",
            Link = "/invitations/abc",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var repository = new Mock<INotificationRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, GetNotifications.MaxItems, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { notification });
        repository.Setup(item => item.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var response = await new GetNotifications(repository.Object).ExecuteAsync(userId);

        var result = response.Items.Should().ContainSingle().Which;
        result.Id.Should().Be(notification.Id);
        result.Type.Should().Be(NotificationType.GroupInvitation);
        result.ReadAt.Should().BeNull();
        response.UnreadCount.Should().Be(3);
    }

    [Fact]
    public async Task GetNotifications_WhenUserHasNoNotifications_ReturnsAnEmptyListAndZeroUnreadCount()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<INotificationRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, GetNotifications.MaxItems, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserNotification>());
        repository.Setup(item => item.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var response = await new GetNotifications(repository.Object).ExecuteAsync(userId);

        response.Items.Should().BeEmpty();
        response.UnreadCount.Should().Be(0);
    }
}
