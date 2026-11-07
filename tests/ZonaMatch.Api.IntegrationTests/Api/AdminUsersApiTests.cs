using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Admin.Responses;

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
            .Which.Should().BeEquivalentTo(new AdminUserResponse(member.User.Id, member.User.Email, member.User.BirthDate, IsAdmin: false));
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
