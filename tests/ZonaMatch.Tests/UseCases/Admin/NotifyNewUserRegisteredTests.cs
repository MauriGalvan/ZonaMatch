using FluentAssertions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Admin;

public class NotifyNewUserRegisteredTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 21, 0, 0, TimeSpan.Zero);

    private readonly User _firstAdministrator = new() { Email = "admin1@example.com", IsAdministrator = true };
    private readonly User _secondAdministrator = new() { Email = "admin2@example.com", IsAdministrator = true };
    private readonly User _member = new() { Email = "bruno@example.com" };
    private readonly FakeNotificationRepository _notifications = new();
    private readonly NotifyNewUserRegistered _useCase;

    public NotifyNewUserRegisteredTests()
    {
        _useCase = new NotifyNewUserRegistered(
            new FakeAdminUserRepository(_firstAdministrator, _secondAdministrator, _member),
            _notifications,
            new FixedTimeProvider(Now));
    }

    private static User NewUser(string email) => new() { Email = email };

    [Fact]
    public async Task Notify_CreatesOneNotificationForEachAdministrator()
    {
        await _useCase.ExecuteAsync(NewUser("ana@example.com"));

        _notifications.Notifications.Select(notification => notification.UserId)
            .Should().BeEquivalentTo([_firstAdministrator.Id, _secondAdministrator.Id]);
        _notifications.Notifications.Should().AllSatisfy(notification =>
        {
            notification.Type.Should().Be(NotificationType.NewUserRegistered);
            notification.Title.Should().Be("Nuevo usuario");
            notification.Message.Should().Contain("ana@example.com");
            notification.Link.Should().Be(NotifyNewUserRegistered.UserListLink).And.Be("/admin/usuarios");
            notification.CreatedAt.Should().Be(Now);
        });
    }

    [Fact]
    public async Task Notify_CreatesANotificationForEveryNewAccount()
    {
        await _useCase.ExecuteAsync(NewUser("ana@example.com"));
        await _useCase.ExecuteAsync(NewUser("carla@example.com"));

        _notifications.Notifications.Where(item => item.UserId == _firstAdministrator.Id)
            .Select(item => item.Message)
            .Should().SatisfyRespectively(
                first => first.Should().Contain("ana@example.com"),
                second => second.Should().Contain("carla@example.com"));
    }

    [Fact]
    public async Task Notify_WhenThereAreNoAdministrators_DoesNotSave()
    {
        var notifications = new FakeNotificationRepository();
        var useCase = new NotifyNewUserRegistered(new FakeAdminUserRepository(_member), notifications, new FixedTimeProvider(Now));

        await useCase.ExecuteAsync(NewUser("ana@example.com"));

        notifications.Notifications.Should().BeEmpty();
        notifications.SaveCount.Should().Be(0);
    }
}
