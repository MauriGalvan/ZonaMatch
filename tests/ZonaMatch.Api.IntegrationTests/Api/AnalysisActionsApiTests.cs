using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AnalysisActionsApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task Analysis_WhenRenamedFavoritedAndRecalculated_PersistsEachAction()
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
            placeName: "Work"));
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createResponse = await owner.Client.PostAsJsonAsync("/api/v1/analyses", new CreateAnalysisRequest
        {
            Name = "Analysis actions",
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
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var original = await createResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        original.Should().NotBeNull();

        var unauthorizedFavoriteResponse = await otherUser.Client.PutAsJsonAsync(
            $"/api/v1/analyses/{original!.Id}/favorite",
            new SetAnalysisFavoriteRequest { IsFavorite = true });
        unauthorizedFavoriteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var renameResponse = await owner.Client.PutAsJsonAsync(
            $"/api/v1/analyses/{original.Id}",
            new RenameAnalysisRequest { Name = "  Renamed analysis  " });
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamedAnalysis = await renameResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        renamedAnalysis.Should().NotBeNull();
        renamedAnalysis!.Name.Should().Be("Renamed analysis");

        var favoriteResponse = await owner.Client.PutAsJsonAsync(
            $"/api/v1/analyses/{original.Id}/favorite",
            new SetAnalysisFavoriteRequest { IsFavorite = true });
        favoriteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var favoriteDetailsResponse = await owner.Client.GetAsync($"/api/v1/analyses/{original.Id}");
        var favoriteDetails = await favoriteDetailsResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        favoriteDetails.Should().NotBeNull();
        favoriteDetails!.Name.Should().Be("Renamed analysis");
        favoriteDetails.IsFavorite.Should().BeTrue();

        var recalculateResponse = await owner.Client.PostAsync(
            $"/api/v1/analyses/{original.Id}/recalculations",
            content: null);
        recalculateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var recalculatedAnalysis = await recalculateResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        recalculatedAnalysis.Should().NotBeNull();
        recalculatedAnalysis!.OriginAnalysisId.Should().Be(original.Id);
        recalculatedAnalysis.Name.Should().Contain("datos al");
        recalculatedAnalysis.Zones.Should().ContainSingle(zone =>
            zone.Slug == IntegrationZoneRepository.ZoneSlug && zone.IsInitial);
        recalculatedAnalysis.Criteria.Should().ContainKey("seguridad").WhoseValue.Should().Be("alta");
        recalculatedAnalysis.Result.Should().BeNull();

        var finalOriginalResponse = await owner.Client.GetAsync($"/api/v1/analyses/{original.Id}");
        var finalOriginal = await finalOriginalResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        finalOriginal.Should().NotBeNull();
        finalOriginal!.Versions.Should().ContainSingle().Which.Id.Should().Be(recalculatedAnalysis.Id);
        finalOriginal.IsFavorite.Should().BeTrue();
    }
}
