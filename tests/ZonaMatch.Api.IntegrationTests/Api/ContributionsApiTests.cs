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
public sealed class ContributionsApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task Contribution_WhenSubmittedAndApprovedByAdmin_PersistsCommunityPoint()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var contributor = await TestApiUser.RegisterAsync(factory);
        using var regularUser = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);

        var slug = IntegrationZoneRepository.CreateContributionZoneSlug();
        var createResponse = await contributor.Client.PostAsJsonAsync(
            $"/api/v1/zones/{slug}/contributions/points",
            new CreatePointContributionRequest(
                Category: "salud",
                Name: "Community Health Center",
                Schedule: "Monday-Friday 09:00-17:00",
                Address: "123 Integration Street",
                Latitude: -34.61,
                Longitude: -58.43,
                LivesOrWorksInZone: true));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var contribution = await createResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        contribution.Should().NotBeNull();
        contribution!.Status.Should().Be(ContributionStatus.Pending);
        contribution.IsMine.Should().BeTrue();
        contribution.Name.Should().Be("Community Health Center");

        var summaryResponse = await contributor.Client.GetAsync($"/api/v1/zones/{slug}/contributions/summary");
        var pendingSummary = await summaryResponse.Content.ReadFromJsonAsync<ContributionSummaryResponse>();
        pendingSummary.Should().NotBeNull();
        pendingSummary!.Total.Should().Be(1);
        pendingSummary.Approved.Should().Be(0);
        pendingSummary.Pending.Should().Be(1);

        var pendingResponse = await regularUser.Client.GetAsync($"/api/v1/zones/{slug}/contributions/pending");
        var pendingContributions = await pendingResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ContributionResponse>>(ApiJson.Options);
        pendingContributions.Should().ContainSingle(item => item.Id == contribution.Id);

        var resolutionPath = $"/api/v1/zones/{slug}/contributions/{contribution.Id}/resolution";
        var forbiddenResolution = await regularUser.Client.PutAsJsonAsync(
            resolutionPath,
            new ResolveContributionRequest(Approve: true));
        forbiddenResolution.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        administrator.AuthenticateAsAdministrator(factory);
        var approveResponse = await administrator.Client.PutAsJsonAsync(
            resolutionPath,
            new ResolveContributionRequest(Approve: true));
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approvedContribution = await approveResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        approvedContribution.Should().NotBeNull();
        approvedContribution!.Status.Should().Be(ContributionStatus.Approved);
        approvedContribution.IsMine.Should().BeFalse();

        var repeatedResolution = await administrator.Client.PutAsJsonAsync(
            resolutionPath,
            new ResolveContributionRequest(Approve: true));
        repeatedResolution.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var finalSummaryResponse = await contributor.Client.GetAsync($"/api/v1/zones/{slug}/contributions/summary");
        var finalSummary = await finalSummaryResponse.Content.ReadFromJsonAsync<ContributionSummaryResponse>();
        finalSummary.Should().NotBeNull();
        finalSummary!.Total.Should().Be(1);
        finalSummary.Approved.Should().Be(1);
        finalSummary.Pending.Should().Be(0);

        var finalPendingResponse = await contributor.Client.GetAsync($"/api/v1/zones/{slug}/contributions/pending");
        var finalPending = await finalPendingResponse.Content.ReadFromJsonAsync<IReadOnlyList<ContributionResponse>>(
            ApiJson.Options);
        finalPending.Should().BeEmpty();

        await using var dbContext = database.CreateDbContext();
        var savedContribution = await dbContext.Contributions.SingleAsync(item => item.Id == contribution.Id);
        savedContribution.Status.Should().Be(ContributionStatus.Approved);
        savedContribution.ResolvedByUserId.Should().Be(administrator.User.Id);
        savedContribution.ResolvedAt.Should().NotBeNull();

        var communityPoi = await dbContext.CommunityPois.SingleAsync(poi =>
            poi.Id == CommunityPoi.NewPointId(contribution.Id));
        communityPoi.IsNew.Should().BeTrue();
        communityPoi.ZoneSlug.Should().Be(slug);
        communityPoi.Name.Should().Be("Community Health Center");
        communityPoi.Category.Should().Be("salud");
        communityPoi.Location.Should().NotBeNull();
        communityPoi.Location!.X.Should().BeApproximately(-58.43, 0.00001);
        communityPoi.Location.Y.Should().BeApproximately(-34.61, 0.00001);
        communityPoi.LastContributionId.Should().Be(contribution.Id);
    }
}
