using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminStatsApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task GetStats_WhenAdministrator_ReturnsStatsIncludingNewUsers()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.GetAsync("/api/v1/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<AdminStatsResponse>();
        stats!.Users.Total.Should().BeGreaterThanOrEqualTo(2);
        stats.Users.NewLast7Days.Should().BeGreaterThanOrEqualTo(2);
        stats.WeeklyRegistrations.Should().HaveCount(GetAdminStats.WeekCount);
        stats.WeeklyRegistrations[^1].Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetStats_WhenNotAdministrator_ReturnsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        var response = await member.Client.GetAsync("/api/v1/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStats_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/api/v1/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
