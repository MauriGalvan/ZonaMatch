using System.Net;
using ZonaMatch.Application.DTOs.SavedZone.Requests;
using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.DTOs.Admin.Responses;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminZonesApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task GetActivity_CountsQuestionsAndSavesOfAZoneAndSortsEachRanking()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();
        // More questions than any other test zone has, so it leads the ranking.
        const int questions = 6;
        for (var index = 0; index < questions; index++)
        {
            (await member.Client.PostAsJsonAsync($"/api/v1/zones/{zoneSlug}/questions", new CreateQuestionRequest($"Pregunta {index + 1}")))
                .StatusCode.Should().Be(HttpStatusCode.Created);
        }
        (await member.Client.PutAsJsonAsync($"/api/v1/saved-zones/{zoneSlug}", new SaveZoneRequest { Name = "Zona de prueba", Subtitle = "Actividad" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await administrator.Client.GetAsync("/api/v1/admin/zones/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var activity = (await response.Content.ReadFromJsonAsync<AdminZoneActivityResponse>())!;
        activity.MostAsked.Should().Contain(zone => zone.ZoneSlug == zoneSlug && zone.Count == questions);
        activity.MostAsked.Select(zone => zone.Count).Should().BeInDescendingOrder();
        activity.MostSaved.Select(zone => zone.Count).Should().BeInDescendingOrder();
        activity.LeastSaved.Select(zone => zone.Count).Should().BeInAscendingOrder().And.OnlyContain(count => count >= 1);
        activity.MostSaved.Should().HaveCountLessThanOrEqualTo(10);
    }

    [Theory]
    [InlineData("/api/v1/admin/zones/activity")]
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
