using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class SavedAnalysisApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task SavedAnalysis_WhenRequestedByAnotherUser_IsNotListedOrAccessible()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var otherUser = await TestApiUser.RegisterAsync(factory);

        var profileResponse = await owner.Client.PutAsJsonAsync("/api/v1/profile", ProfileApiTests.CreateProfileRequest(
            peopleCount: 1,
            personNames: [],
            ageRanges: [],
            criterion: "seguridad",
            priority: "alta",
            placeName: "Trabajo"));
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createAnalysisResponse = await owner.Client.PostAsJsonAsync("/api/v1/analyses", new CreateAnalysisRequest
        {
            Name = "Integration analysis",
            Zones =
            [
                new AnalysisZoneRequest
                {
                    Slug = IntegrationZoneRepository.ZoneSlug,
                    Name = "Integration Test Zone",
                    IsInitial = true
                }
            ]
        });

        createAnalysisResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var analysis = await createAnalysisResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        analysis.Should().NotBeNull();

        var ownerReadResponse = await owner.Client.GetAsync($"/api/v1/analyses/{analysis!.Id}");
        ownerReadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var otherUserReadResponse = await otherUser.Client.GetAsync($"/api/v1/analyses/{analysis.Id}");
        otherUserReadResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var otherUserVersionResponse = await otherUser.Client.PostAsJsonAsync(
            $"/api/v1/analyses/{analysis.Id}/versions",
            new CreateAnalysisVersionRequest
            {
                Criterion = ProfileApiTests.CreateProfileRequest(
                    peopleCount: 1,
                    personNames: [],
                    ageRanges: [],
                    criterion: "seguridad",
                    priority: "baja",
                    placeName: "Unauthorized place")
            });
        otherUserVersionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var otherUserListResponse = await otherUser.Client.GetAsync("/api/v1/analyses");
        otherUserListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var otherUserAnalyses = await otherUserListResponse.Content.ReadFromJsonAsync<IReadOnlyList<AnalysisResponse>>();
        otherUserAnalyses.Should().BeEmpty();

        var otherUserDeleteResponse = await otherUser.Client.DeleteAsync($"/api/v1/analyses/{analysis.Id}");
        otherUserDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerReadAfterAttempt = await owner.Client.GetAsync($"/api/v1/analyses/{analysis.Id}");
        ownerReadAfterAttempt.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
