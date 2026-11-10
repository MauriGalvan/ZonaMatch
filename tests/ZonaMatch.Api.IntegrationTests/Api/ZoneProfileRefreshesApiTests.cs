using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Zone.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class ZoneProfileRefreshesApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task Refresh_WhenRequestedByAnAdministrator_RebuildsTheProfilesAndReturnsHowManyZones()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var admin = await TestApiUser.RegisterAsync(factory);
        admin.AuthenticateAsAdministrator(factory);

        var response = await admin.Client.PostAsync("/api/v1/zone-profile-refreshes", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refresh = await response.Content.ReadFromJsonAsync<ZoneProfilesRefreshResponse>();
        refresh!.Zones.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Refresh_WhenRequestedByAUserWhoIsNotAdministrator_IsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var user = await TestApiUser.RegisterAsync(factory);

        var response = await user.Client.PostAsync("/api/v1/zone-profile-refreshes", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
