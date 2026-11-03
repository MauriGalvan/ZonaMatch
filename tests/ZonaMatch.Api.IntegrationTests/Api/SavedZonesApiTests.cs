using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.SavedZone.Requests;
using ZonaMatch.Application.DTOs.SavedZone.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class SavedZonesApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task SavedZone_WhenCreatedUpdatedAndDeleted_IsScopedToCurrentUser()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var otherUser = await TestApiUser.RegisterAsync(factory);
        var slug = IntegrationZoneRepository.ZoneSlug;

        var initialOtherUserZones = await otherUser.Client.GetFromJsonAsync<IReadOnlyList<SavedZoneResponse>>(
            "/api/v1/saved-zones");
        initialOtherUserZones.Should().BeEmpty();

        var createResponse = await owner.Client.PutAsJsonAsync(
            $"/api/v1/saved-zones/{slug}",
            new SaveZoneRequest { Name = "Integration Test Zone", Subtitle = "First note" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdZone = await createResponse.Content.ReadFromJsonAsync<SavedZoneResponse>();
        createdZone.Should().NotBeNull();
        createdZone!.Id.Should().Be(slug);
        createdZone.Name.Should().Be("Integration Test Zone");
        createdZone.Subtitle.Should().Be("First note");

        var updateResponse = await owner.Client.PutAsJsonAsync(
            $"/api/v1/saved-zones/{slug}",
            new SaveZoneRequest { Name = "Renamed zone", Subtitle = "Updated note" });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerZones = await owner.Client.GetFromJsonAsync<IReadOnlyList<SavedZoneResponse>>(
            "/api/v1/saved-zones");
        ownerZones.Should().ContainSingle();
        ownerZones![0].Id.Should().Be(slug);
        ownerZones[0].Name.Should().Be("Renamed zone");
        ownerZones[0].Subtitle.Should().Be("Updated note");

        var otherUserDeleteResponse = await otherUser.Client.DeleteAsync($"/api/v1/saved-zones/{slug}");
        otherUserDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.GetFromJsonAsync<IReadOnlyList<SavedZoneResponse>>("/api/v1/saved-zones"))
            .Should().ContainSingle(zone => zone.Id == slug);

        var deleteResponse = await owner.Client.DeleteAsync($"/api/v1/saved-zones/{slug}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.GetFromJsonAsync<IReadOnlyList<SavedZoneResponse>>("/api/v1/saved-zones"))
            .Should().BeEmpty();

        (await owner.Client.DeleteAsync($"/api/v1/saved-zones/{slug}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }
}
