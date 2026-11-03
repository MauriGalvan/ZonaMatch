using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AnalysisVersionsApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task AnalysisVersions_WhenCreatedFromProfileSnapshot_StayIndependentAndDeleteWithOriginal()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var user = await TestApiUser.RegisterAsync(factory);

        var profileResponse = await user.Client.PutAsJsonAsync("/api/v1/profile", ProfileApiTests.CreateProfileRequest(
            peopleCount: 1,
            personNames: [],
            ageRanges: [],
            criterion: "seguridad",
            priority: "alta",
            placeName: "Original place"));
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createAnalysisResponse = await user.Client.PostAsJsonAsync("/api/v1/analyses", new CreateAnalysisRequest
        {
            Name = "Original analysis",
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
        var original = await createAnalysisResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        original.Should().NotBeNull();

        var updatedProfileResponse = await user.Client.PutAsJsonAsync("/api/v1/profile", ProfileApiTests.CreateProfileRequest(
            peopleCount: 1,
            personNames: [],
            ageRanges: [],
            criterion: "salud",
            priority: "media",
            placeName: "Updated profile place"));
        updatedProfileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var versionRequest = new CreateAnalysisVersionRequest
        {
            Name = "Adjusted analysis",
            Criterion = ProfileApiTests.CreateProfileRequest(
                peopleCount: 1,
                personNames: [],
                ageRanges: [],
                criterion: "seguridad",
                priority: "baja",
                placeName: "Adjusted place")
        };
        var createVersionResponse = await user.Client.PostAsJsonAsync(
            $"/api/v1/analyses/{original!.Id}/versions",
            versionRequest);
        createVersionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = await createVersionResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        version.Should().NotBeNull();
        version!.OriginAnalysisId.Should().Be(original.Id);
        version.Criteria.Should().ContainKey("seguridad").WhoseValue.Should().Be("baja");
        version.Points.Places.Should().ContainSingle().Which.Name.Should().Be("Adjusted place");

        var secondVersionResponse = await user.Client.PostAsJsonAsync(
            $"/api/v1/analyses/{version.Id}/versions",
            new CreateAnalysisVersionRequest
            {
                Criterion = ProfileApiTests.CreateProfileRequest(
                    peopleCount: 1,
                    personNames: [],
                    ageRanges: [],
                    criterion: "seguridad",
                    priority: "media",
                    placeName: "Second adjustment")
            });
        secondVersionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondVersion = await secondVersionResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        secondVersion.Should().NotBeNull();
        secondVersion!.OriginAnalysisId.Should().Be(original.Id);

        var originalDetailsResponse = await user.Client.GetAsync($"/api/v1/analyses/{original.Id}");
        originalDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var originalDetails = await originalDetailsResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        originalDetails.Should().NotBeNull();
        originalDetails!.Criteria.Should().ContainKey("seguridad").WhoseValue.Should().Be("alta");
        originalDetails.Versions.Should().HaveCount(2)
            .And.Contain(versionItem => versionItem.Id == version.Id)
            .And.Contain(versionItem => versionItem.Id == secondVersion.Id);
        originalDetails.Zones.Should().ContainSingle(zone => zone.IsInitial);

        var savedProfileResponse = await user.Client.GetAsync("/api/v1/profile");
        var savedProfile = await savedProfileResponse.Content.ReadFromJsonAsync<ZonaMatch.Application.DTOs.Profile.Responses.ProfileResponse>();
        savedProfile.Should().NotBeNull();
        savedProfile!.Criteria.Should().ContainKey("salud").WhoseValue.Should().Be("media");

        var deleteOriginalResponse = await user.Client.DeleteAsync($"/api/v1/analyses/{original.Id}");
        deleteOriginalResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await user.Client.GetAsync($"/api/v1/analyses/{original.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await user.Client.GetAsync($"/api/v1/analyses/{version.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await user.Client.GetAsync($"/api/v1/analyses/{secondVersion.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        await using var dbContext = database.CreateDbContext();
        (await dbContext.Analyses.CountAsync(analysis => analysis.UserId == user.User.Id)).Should().Be(0);
        (await dbContext.Contexts.CountAsync(context =>
            context.UserId == user.User.Id && context.Type == ZonaMatch.Domain.Entities.ContextType.Analysis))
            .Should().Be(0);
        var analysisIds = new[] { original.Id, version.Id, secondVersion.Id };
        (await dbContext.AnalysisZones.CountAsync(zone => analysisIds.Contains(zone.AnalysisId))).Should().Be(0);
    }
}
