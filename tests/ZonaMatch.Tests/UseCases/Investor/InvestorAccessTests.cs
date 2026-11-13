using FluentAssertions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Application.UseCases.Investor;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Tests.UseCases.Admin;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Investor;

public class InvestorAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);
    private readonly User _administrator = new() { Email = "admin@example.com", IsAdministrator = true };
    private readonly User _member = new() { Email = "ana@example.com" };
    private readonly FakeAdminUserRepository _users;
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FixedTimeProvider _clock = new(Now);

    public InvestorAccessTests()
    {
        _users = new FakeAdminUserRepository(_administrator, _member);
    }

    [Fact]
    public async Task Request_WhenTheUserHasNoAccess_SavesTheRequestAndNotifiesTheAdministrators()
    {
        var response = await new RequestInvestorAccess(_users, _notifications, _clock).ExecuteAsync(_member.Id);

        response.Status.Should().Be(InvestorAccessStatus.Requested);
        response.RequestedAt.Should().Be(Now);
        _member.InvestorAccess.Should().Be(InvestorAccess.Requested);
        var notification = _notifications.Notifications.Should().ContainSingle().Subject;
        notification.UserId.Should().Be(_administrator.Id);
        notification.Type.Should().Be(NotificationType.InvestorAccessRequested);
        notification.Message.Should().Contain("ana@example.com");
    }

    [Fact]
    public async Task Request_WhenAlreadyRequested_KeepsTheFirstDateAndDoesNotNotifyAgain()
    {
        var firstDate = Now.AddDays(-2);
        _member.InvestorAccess = InvestorAccess.Requested;
        _member.InvestorAccessRequestedAt = firstDate;

        var response = await new RequestInvestorAccess(_users, _notifications, _clock).ExecuteAsync(_member.Id);

        response.RequestedAt.Should().Be(firstDate);
        _notifications.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_WhenAlreadyGranted_StaysGranted()
    {
        _member.InvestorAccess = InvestorAccess.Granted;

        var response = await new RequestInvestorAccess(_users, _notifications, _clock).ExecuteAsync(_member.Id);

        response.Status.Should().Be(InvestorAccessStatus.Granted);
        _member.InvestorAccess.Should().Be(InvestorAccess.Granted);
    }

    [Fact]
    public async Task Update_WhenGranted_EnablesTheAccessAndNotifiesTheUser()
    {
        _member.InvestorAccess = InvestorAccess.Requested;

        var response = await new UpdateInvestorAccess(_users, _notifications, _clock).ExecuteAsync(_member.Id, granted: true);

        response.InvestorAccess.Should().Be(InvestorAccessStatus.Granted);
        _member.InvestorAccessGrantedAt.Should().Be(Now);
        var notification = _notifications.Notifications.Should().ContainSingle().Subject;
        notification.UserId.Should().Be(_member.Id);
        notification.Type.Should().Be(NotificationType.InvestorAccessGranted);
        notification.Link.Should().Be(UpdateInvestorAccess.InvestorProfileLink);
    }

    [Fact]
    public async Task Update_WhenRemoved_ClearsTheAccess()
    {
        _member.InvestorAccess = InvestorAccess.Granted;
        _member.InvestorAccessGrantedAt = Now.AddDays(-1);

        var response = await new UpdateInvestorAccess(_users, _notifications, _clock).ExecuteAsync(_member.Id, granted: false);

        response.InvestorAccess.Should().Be(InvestorAccessStatus.None);
        _member.InvestorAccess.Should().BeNull();
        _member.InvestorAccessGrantedAt.Should().BeNull();
        _notifications.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ListUsers_ShowsEachUsersInvestorAccess()
    {
        _member.InvestorAccess = InvestorAccess.Requested;
        _member.InvestorAccessRequestedAt = Now;

        var users = await new ListUsers(_users).ExecuteAsync();

        users.Should().ContainSingle(user => user.Email == "ana@example.com")
            .Which.Should().Match<ZonaMatch.Application.DTOs.Admin.Responses.AdminUserResponse>(user =>
                user.InvestorAccess == InvestorAccessStatus.Requested && user.InvestorAccessRequestedAt == Now);
        users.Should().ContainSingle(user => user.Email == "admin@example.com")
            .Which.InvestorAccess.Should().Be(InvestorAccessStatus.None);
    }
}
