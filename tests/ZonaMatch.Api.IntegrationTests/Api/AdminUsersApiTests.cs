using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Admin.Requests;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.DTOs.Notification.Responses;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminUsersApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task ListUsers_WhenAdministrator_ReturnsRegisteredUsers()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<AdminUserResponse>>();
        users.Should().ContainSingle(user => user.Id == member.User.Id)
            .Which.Should().BeEquivalentTo(
                new AdminUserResponse(member.User.Id, member.User.Email, member.User.BirthDate, IsAdmin: false),
                options => options.Excluding(user => user.RegisteredAt));
    }

    [Fact]
    public async Task ListUsers_WhenAdministrator_ReturnsTheNewestAccountFirst()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var older = await TestApiUser.RegisterAsync(factory);
        using var newer = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await response.Content.ReadFromJsonAsync<List<AdminUserResponse>>())!.Select(user => user.Id).ToList();
        ids.Should().StartWith([newer.User.Id, older.User.Id, administrator.User.Id], "the last account registered is the first one listed");
    }

    [Fact]
    public async Task UpdateRole_WhenAdministratorGrantsAdmin_PersistsTheRole()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.PutAsJsonAsync(
            $"/api/v1/admin/users/{member.User.Id}/role", new UpdateUserRoleRequest(IsAdmin: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AdminUserResponse>())!.IsAdmin.Should().BeTrue();

        var users = await administrator.Client.GetFromJsonAsync<List<AdminUserResponse>>("/api/v1/admin/users");
        users.Should().ContainSingle(user => user.Id == member.User.Id).Which.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRole_WhenAdministratorGrantsAdmin_NotifiesTheUserWithALinkToTheAdminDashboard()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        (await administrator.Client.PutAsJsonAsync(
            $"/api/v1/admin/users/{member.User.Id}/role", new UpdateUserRoleRequest(IsAdmin: true)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await member.Client.GetAsync("/api/v1/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await response.Content.ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        notifications!.Items.Should().ContainSingle(notification => notification.Type == NotificationType.AdministratorRoleGranted)
            .Which.Link.Should().Be(UpdateUserRole.AdminDashboardLink);
    }

    [Fact]
    public async Task Register_WhenThereIsAnAdministrator_NotifiesItWithALinkToTheUserList()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        // The administrators lookup reads the database, not the token.
        await using (var dbContext = database.CreateDbContext())
        {
            await dbContext.Users
                .Where(user => user.Id == administrator.User.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsAdministrator, true));
        }

        using var newUser = await TestApiUser.RegisterAsync(factory);

        var response = await administrator.Client.GetAsync("/api/v1/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await response.Content.ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        var notification = notifications!.Items.Should().ContainSingle(item => item.Type == NotificationType.NewUserRegistered).Subject;
        notification.Link.Should().Be(NotifyNewUserRegistered.UserListLink);
        notification.Message.Should().Contain(newUser.User.Email);
    }

    [Fact]
    public async Task ListUsers_WhenAdministrator_ReturnsTheRegistrationDate()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var users = await administrator.Client.GetFromJsonAsync<List<AdminUserResponse>>("/api/v1/admin/users");

        users.Should().ContainSingle(user => user.Id == member.User.Id)
            .Which.RegisteredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task UpdateRole_WhenAdministratorRemovesOwnRole_ReturnsBadRequest()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.PutAsJsonAsync(
            $"/api/v1/admin/users/{administrator.User.Id}/role", new UpdateUserRoleRequest(IsAdmin: false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRole_WhenNotAdministrator_ReturnsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        var response = await member.Client.PutAsJsonAsync(
            $"/api/v1/admin/users/{member.User.Id}/role", new UpdateUserRoleRequest(IsAdmin: true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateRole_WhenUserDoesNotExist_ReturnsNotFound()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.PutAsJsonAsync(
            $"/api/v1/admin/users/{Guid.NewGuid()}/role", new UpdateUserRoleRequest(IsAdmin: true));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateRole_WhenIsAdminIsMissing_ReturnsBadRequest()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.PutAsJsonAsync($"/api/v1/admin/users/{member.User.Id}/role", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListUsers_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_WhenNotAdministrator_ReturnsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        var response = await member.Client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
