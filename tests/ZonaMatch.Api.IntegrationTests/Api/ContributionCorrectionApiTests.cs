using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Contribution.Requests;
using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class ContributionCorrectionApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task ContributionCorrection_WhenApprovedByAdministrator_UpdatesCommunityPoiOverlay()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var contributor = await TestApiUser.RegisterAsync(factory);
        using var regularUser = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        var zoneSlug = IntegrationZoneRepository.CreateContributionZoneSlug();
        var pointOfInterestId = CreateOsmPointOfInterestId();

        var createResponse = await contributor.Client.PostAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/contributions/corrections",
            CreateNameCorrection(pointOfInterestId),
            ApiJson.Options);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var correction = await createResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        correction.Should().NotBeNull();
        correction!.Type.Should().Be(ContributionType.Correction);
        correction.Status.Should().Be(ContributionStatus.Pending);
        correction.IsMine.Should().BeTrue();
        correction.PointOfInterestId.Should().Be(pointOfInterestId);
        correction.PointOfInterestName.Should().Be("Old Community Library");
        correction.Reason.Should().Be(CorrectionReason.Name);
        correction.Name.Should().Be("Updated Community Library");

        var pendingResponse = await regularUser.Client.GetAsync($"/api/v1/zones/{zoneSlug}/contributions/pending");
        pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingCorrections = await pendingResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ContributionResponse>>(ApiJson.Options);
        pendingCorrections.Should().ContainSingle(item => item.Id == correction.Id);

        var resolutionPath = $"/api/v1/zones/{zoneSlug}/contributions/{correction.Id}/resolution";
        var forbiddenResolution = await regularUser.Client.PutAsJsonAsync(
            resolutionPath,
            new ResolveContributionRequest(Approve: true));
        forbiddenResolution.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        administrator.AuthenticateAsAdministrator(factory);
        var resolutionResponse = await administrator.Client.PutAsJsonAsync(
            resolutionPath,
            new ResolveContributionRequest(Approve: true));
        resolutionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedCorrection = await resolutionResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        resolvedCorrection.Should().NotBeNull();
        resolvedCorrection!.Status.Should().Be(ContributionStatus.Approved);
        resolvedCorrection.IsMine.Should().BeFalse();

        var repeatedResolution = await administrator.Client.PutAsJsonAsync(
            resolutionPath,
            new ResolveContributionRequest(Approve: true));
        repeatedResolution.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var finalPendingResponse = await regularUser.Client.GetAsync($"/api/v1/zones/{zoneSlug}/contributions/pending");
        var finalPendingCorrections = await finalPendingResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ContributionResponse>>(ApiJson.Options);
        finalPendingCorrections.Should().NotContain(item => item.Id == correction.Id);

        await using var verificationContext = database.CreateDbContext();
        var savedCorrection = await verificationContext.Contributions.SingleAsync(item => item.Id == correction.Id);
        savedCorrection.Type.Should().Be(ContributionType.Correction);
        savedCorrection.Status.Should().Be(ContributionStatus.Approved);
        savedCorrection.ResolvedByUserId.Should().Be(administrator.User.Id);
        savedCorrection.ResolvedAt.Should().NotBeNull();

        var communityPoi = await verificationContext.CommunityPois.SingleAsync(item => item.Id == pointOfInterestId);
        communityPoi.IsNew.Should().BeFalse();
        communityPoi.ZoneSlug.Should().Be(zoneSlug);
        communityPoi.Name.Should().Be("Updated Community Library");
        communityPoi.LastContributionId.Should().Be(correction.Id);
        communityPoi.Removed.Should().BeFalse();
    }

    [Fact]
    public async Task ContributionCorrection_WhenRejectedByAdministrator_DoesNotCreatePoiOverlay()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var contributor = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        var zoneSlug = IntegrationZoneRepository.CreateContributionZoneSlug();
        var pointOfInterestId = CreateOsmPointOfInterestId();

        var createResponse = await contributor.Client.PostAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/contributions/corrections",
            CreateNameCorrection(pointOfInterestId),
            ApiJson.Options);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var correction = await createResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        correction.Should().NotBeNull();

        administrator.AuthenticateAsAdministrator(factory);
        var resolutionResponse = await administrator.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/contributions/{correction!.Id}/resolution",
            new ResolveContributionRequest(Approve: false));
        resolutionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedCorrection = await resolutionResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        resolvedCorrection.Should().NotBeNull();
        resolvedCorrection!.Status.Should().Be(ContributionStatus.Rejected);

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.CommunityPois.CountAsync(item => item.Id == pointOfInterestId)).Should().Be(0);
        var savedCorrection = await verificationContext.Contributions.SingleAsync(item => item.Id == correction.Id);
        savedCorrection.Status.Should().Be(ContributionStatus.Rejected);
        savedCorrection.ResolvedByUserId.Should().Be(administrator.User.Id);
        savedCorrection.ResolvedAt.Should().NotBeNull();
    }

    private static CreateContributionCorrectionRequest CreateNameCorrection(string pointOfInterestId) => new(
        PointOfInterestId: pointOfInterestId,
        PointOfInterestName: "Old Community Library",
        Reason: CorrectionReason.Name,
        ProposedName: "Updated Community Library",
        ProposedCategory: null,
        Latitude: null,
        Longitude: null,
        Comment: "The name on the map is outdated.");

    private static string CreateOsmPointOfInterestId() =>
        $"n{Random.Shared.NextInt64(1, 9_000_000_000_000_000_000).ToString(CultureInfo.InvariantCulture)}";
}
