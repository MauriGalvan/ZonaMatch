using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminZonesApiTests(PostgreSqlDatabaseFixture database)
{
    [Theory]
    [InlineData("/api/v1/admin/zones")]
    [InlineData("/api/v1/admin/zones/palermo")]
    public async Task GetZones_WhenNotAdministrator_ReturnsForbidden(string path)
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        var response = await member.Client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/v1/admin/zones")]
    [InlineData("/api/v1/admin/zones/palermo")]
    public async Task GetZones_WhenAnonymous_ReturnsUnauthorized(string path)
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
