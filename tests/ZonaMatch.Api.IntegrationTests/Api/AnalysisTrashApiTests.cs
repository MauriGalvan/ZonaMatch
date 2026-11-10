using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.UseCases.SavedAnalysis;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AnalysisTrashApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task AnalysisTrash_WhenVersionIsPermanentlyDeleted_LeavesOriginalAndOtherVersions()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var user = await TestApiUser.RegisterAsync(factory);
        var original = await CreateAnalysisAsync(user, "Analysis with versions");
        var firstVersion = await CreateVersionAsync(user, original.Id, "Version to delete");
        var secondVersion = await CreateVersionAsync(user, original.Id, "Version to keep");

        var moveToTrashResponse = await user.Client.DeleteAsync($"/api/v1/analyses/{firstVersion.Id}");
        moveToTrashResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var permanentlyDeleteResponse = await user.Client.DeleteAsync($"/api/v1/analyses/trash/{firstVersion.Id}");
        permanentlyDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var originalResponse = await user.Client.GetAsync($"/api/v1/analyses/{original.Id}");
        originalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var originalDetails = await originalResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        originalDetails.Should().NotBeNull();
        originalDetails!.Versions.Should().ContainSingle().Which.Id.Should().Be(secondVersion.Id);
        (await user.Client.GetAsync($"/api/v1/analyses/{firstVersion.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var trashResponse = await user.Client.GetAsync("/api/v1/analyses/trash");
        var trashedAnalyses = await trashResponse.Content.ReadFromJsonAsync<IReadOnlyList<TrashedAnalysisResponse>>();
        trashResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trashedAnalyses.Should().NotBeNull();
        trashedAnalyses!.Should().BeEmpty();

        await using var dbContext = database.CreateDbContext();
        (await dbContext.Analyses.CountAsync(analysis => analysis.UserId == user.User.Id)).Should().Be(2);
        (await dbContext.Contexts.CountAsync(context =>
            context.UserId == user.User.Id && context.Type == ZonaMatch.Domain.Entities.ContextType.Analysis))
            .Should().Be(2);
        (await dbContext.AnalysisZones.CountAsync(zone => zone.AnalysisId == firstVersion.Id)).Should().Be(0);
    }

    [Fact]
    public async Task AnalysisTrash_WhenEmptied_DeletesOnlyTheCurrentUsersTrashedAnalyses()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var otherUser = await TestApiUser.RegisterAsync(factory);
        var ownerAnalysis = await CreateAnalysisAsync(owner, "Owner analysis");
        var ownerVersion = await CreateVersionAsync(owner, ownerAnalysis.Id, "Owner version");
        var ownerActiveAnalysis = await CreateAnalysisAsync(owner, "Owner active analysis");
        var otherUserAnalysis = await CreateAnalysisAsync(otherUser, "Other user analysis");

        (await owner.Client.DeleteAsync($"/api/v1/analyses/{ownerAnalysis.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await otherUser.Client.DeleteAsync($"/api/v1/analyses/{otherUserAnalysis.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var unauthorizedPermanentDeleteResponse = await owner.Client.DeleteAsync(
            $"/api/v1/analyses/trash/{otherUserAnalysis.Id}");
        unauthorizedPermanentDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var emptyTrashResponse = await owner.Client.DeleteAsync("/api/v1/analyses/trash");
        emptyTrashResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ownerTrashResponse = await owner.Client.GetAsync("/api/v1/analyses/trash");
        var ownerTrash = await ownerTrashResponse.Content.ReadFromJsonAsync<IReadOnlyList<TrashedAnalysisResponse>>();
        ownerTrashResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ownerTrash.Should().NotBeNull();
        ownerTrash!.Should().BeEmpty();

        var ownerActiveAnalysesResponse = await owner.Client.GetAsync("/api/v1/analyses");
        var ownerActiveAnalyses = await ownerActiveAnalysesResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<AnalysisResponse>>();
        ownerActiveAnalysesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ownerActiveAnalyses.Should().ContainSingle().Which.Id.Should().Be(ownerActiveAnalysis.Id);

        var otherUserTrashResponse = await otherUser.Client.GetAsync("/api/v1/analyses/trash");
        var otherUserTrash = await otherUserTrashResponse.Content.ReadFromJsonAsync<IReadOnlyList<TrashedAnalysisResponse>>();
        otherUserTrashResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        otherUserTrash!.Should().ContainSingle().Which.Id.Should().Be(otherUserAnalysis.Id);

        await using var dbContext = database.CreateDbContext();
        (await dbContext.Analyses.CountAsync(analysis => analysis.UserId == owner.User.Id)).Should().Be(1);
        (await dbContext.Contexts.CountAsync(context =>
            context.UserId == owner.User.Id && context.Type == ZonaMatch.Domain.Entities.ContextType.Analysis))
            .Should().Be(1);
        (await dbContext.Analyses.CountAsync(analysis =>
            analysis.UserId == otherUser.User.Id && analysis.DeletedAt != null)).Should().Be(1);
        (await dbContext.Analyses.AnyAsync(analysis => analysis.Id == ownerActiveAnalysis.Id)).Should().BeTrue();
        (await dbContext.Analyses.AnyAsync(analysis => analysis.Id == ownerAnalysis.Id)).Should().BeFalse();
        (await dbContext.Analyses.AnyAsync(analysis => analysis.Id == ownerVersion.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task AnalysisTrash_WhenRetentionPeriodExpires_HidesAndPermanentlyPurgesTheAnalysis()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var user = await TestApiUser.RegisterAsync(factory);
        var analysis = await CreateAnalysisAsync(user, "Expired analysis");
        var activeAnalysis = await CreateAnalysisAsync(user, "Still active analysis");
        Guid contextId;
        await using (var context = database.CreateDbContext())
            contextId = await context.Analyses
                .Where(item => item.Id == analysis.Id)
                .Select(item => item.ContextId)
                .SingleAsync();

        (await user.Client.DeleteAsync($"/api/v1/analyses/{analysis.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        await using (var dbContext = database.CreateDbContext())
        {
            var expiredAt = DateTimeOffset.UtcNow.AddDays(-31);
            await dbContext.Analyses
                .Where(item => item.Id == analysis.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeletedAt, expiredAt));
        }

        var trashResponse = await user.Client.GetAsync("/api/v1/analyses/trash");
        var trash = await trashResponse.Content.ReadFromJsonAsync<IReadOnlyList<TrashedAnalysisResponse>>();
        trashResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trash!.Should().BeEmpty();

        using (var scope = factory.Services.CreateScope())
        {
            var purgeExpiredAnalyses = scope.ServiceProvider.GetRequiredService<PurgeExpiredAnalyses>();
            (await purgeExpiredAnalyses.ExecuteAsync()).Should().Be(1);
        }

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.Analyses.AnyAsync(item => item.Id == analysis.Id)).Should().BeFalse();
        (await verificationContext.Analyses.AnyAsync(item => item.Id == activeAnalysis.Id)).Should().BeTrue();
        (await verificationContext.Contexts.AnyAsync(context => context.Id == contextId)).Should().BeFalse();
    }

    private static async Task<AnalysisResponse> CreateAnalysisAsync(TestApiUser user, string name)
    {
        var profileResponse = await user.Client.PutAsJsonAsync(
            "/api/v1/profile",
            ProfileApiTests.CreateProfileRequest(
                peopleCount: 1,
                personNames: [],
                ageRanges: [],
                criterion: "seguridad",
                priority: "alta",
                placeName: "Work"));
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createResponse = await user.Client.PostAsJsonAsync("/api/v1/analyses", new CreateAnalysisRequest
        {
            Name = name,
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
        var analysis = await createResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        analysis.Should().NotBeNull();
        return analysis!;
    }

    private static async Task<AnalysisResponse> CreateVersionAsync(TestApiUser user, Guid originalId, string name)
    {
        var createVersionResponse = await user.Client.PostAsJsonAsync(
            $"/api/v1/analyses/{originalId}/versions",
            new CreateAnalysisVersionRequest
            {
                Name = name,
                Criterion = ProfileApiTests.CreateProfileRequest(
                    peopleCount: 1,
                    personNames: [],
                    ageRanges: [],
                    criterion: "seguridad",
                    priority: "alta",
                    placeName: name)
            });
        createVersionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = await createVersionResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        version.Should().NotBeNull();
        return version!;
    }
}
