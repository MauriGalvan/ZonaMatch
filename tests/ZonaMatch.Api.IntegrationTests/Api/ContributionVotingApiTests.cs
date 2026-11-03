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
public sealed class ContributionVotingApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task ContributionVoting_WhenThreeNeighborsConfirm_ApprovesAndPublishesPoint()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var firstVoter = await TestApiUser.RegisterAsync(factory);
        using var secondVoter = await TestApiUser.RegisterAsync(factory);
        using var thirdVoter = await TestApiUser.RegisterAsync(factory);
        var slug = IntegrationZoneRepository.CreateContributionZoneSlug();

        var contribution = await CreateContributionAsync(author.Client, slug);
        var selfVoteResponse = await VoteAsync(author.Client, slug, contribution.Id, confirm: true);
        selfVoteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var ownPendingResponse = await author.Client.GetAsync($"/api/v1/zones/{slug}/contributions/pending");
        var ownPending = await ownPendingResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<ContributionResponse>>(ApiJson.Options);
        ownPending.Should().ContainSingle(item => item.Id == contribution.Id && item.Confirmations == 0);

        var firstVoteResponse = await VoteAsync(firstVoter.Client, slug, contribution.Id, confirm: true);
        firstVoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstVote = await firstVoteResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        firstVote.Should().NotBeNull();
        firstVote!.Status.Should().Be(ContributionStatus.Pending);
        firstVote.Confirmations.Should().Be(1);
        firstVote.MyVote.Should().BeTrue();
        await CommunityPointCountAsync(contribution.Id, expectedCount: 0);

        var secondVoteResponse = await VoteAsync(secondVoter.Client, slug, contribution.Id, confirm: true);
        secondVoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondVote = await secondVoteResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        secondVote.Should().NotBeNull();
        secondVote!.Status.Should().Be(ContributionStatus.Pending);
        secondVote.Confirmations.Should().Be(2);
        await CommunityPointCountAsync(contribution.Id, expectedCount: 0);

        var thirdVoteResponse = await VoteAsync(thirdVoter.Client, slug, contribution.Id, confirm: true);
        thirdVoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approvedContribution = await thirdVoteResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        approvedContribution.Should().NotBeNull();
        approvedContribution!.Status.Should().Be(ContributionStatus.Approved);
        approvedContribution.Confirmations.Should().Be(3);
        approvedContribution.Rejections.Should().Be(0);

        var repeatedVoteResponse = await VoteAsync(firstVoter.Client, slug, contribution.Id, confirm: false);
        repeatedVoteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var dbContext = database.CreateDbContext();
        var savedContribution = await dbContext.Contributions.SingleAsync(item => item.Id == contribution.Id);
        savedContribution.Status.Should().Be(ContributionStatus.Approved);
        (await dbContext.ContributionVotes.CountAsync(vote => vote.ContributionId == contribution.Id))
            .Should().Be(3);

        var communityPoi = await dbContext.CommunityPois.SingleAsync(poi =>
            poi.Id == CommunityPoi.NewPointId(contribution.Id));
        communityPoi.Name.Should().Be("Community Clinic");
        communityPoi.Category.Should().Be("salud");
        communityPoi.LastContributionId.Should().Be(contribution.Id);
    }

    [Fact]
    public async Task ContributionVoting_WhenVoterChangesVote_UpdatesVoteAndRejectionDoesNotPublishPoint()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var changingVoter = await TestApiUser.RegisterAsync(factory);
        using var secondVoter = await TestApiUser.RegisterAsync(factory);
        using var thirdVoter = await TestApiUser.RegisterAsync(factory);
        var slug = IntegrationZoneRepository.CreateContributionZoneSlug();

        var contribution = await CreateContributionAsync(author.Client, slug);

        var initialVoteResponse = await VoteAsync(changingVoter.Client, slug, contribution.Id, confirm: true);
        initialVoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var changedVoteResponse = await VoteAsync(changingVoter.Client, slug, contribution.Id, confirm: false);
        changedVoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var changedVote = await changedVoteResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        changedVote.Should().NotBeNull();
        changedVote!.Status.Should().Be(ContributionStatus.Pending);
        changedVote.Confirmations.Should().Be(0);
        changedVote.Rejections.Should().Be(1);
        changedVote.MyVote.Should().BeFalse();

        var secondVoteResponse = await VoteAsync(secondVoter.Client, slug, contribution.Id, confirm: false);
        secondVoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondVote = await secondVoteResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        secondVote.Should().NotBeNull();
        secondVote!.Status.Should().Be(ContributionStatus.Pending);
        secondVote.Rejections.Should().Be(2);
        await CommunityPointCountAsync(contribution.Id, expectedCount: 0);

        var thirdVoteResponse = await VoteAsync(thirdVoter.Client, slug, contribution.Id, confirm: false);
        thirdVoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejectedContribution = await thirdVoteResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        rejectedContribution.Should().NotBeNull();
        rejectedContribution!.Status.Should().Be(ContributionStatus.Rejected);
        rejectedContribution.Rejections.Should().Be(3);

        var pendingResponse = await author.Client.GetAsync($"/api/v1/zones/{slug}/contributions/pending");
        var pending = await pendingResponse.Content.ReadFromJsonAsync<IReadOnlyList<ContributionResponse>>(ApiJson.Options);
        pending.Should().NotContain(item => item.Id == contribution.Id);

        await using var dbContext = database.CreateDbContext();
        var savedContribution = await dbContext.Contributions.SingleAsync(item => item.Id == contribution.Id);
        savedContribution.Status.Should().Be(ContributionStatus.Rejected);
        (await dbContext.ContributionVotes.CountAsync(vote => vote.ContributionId == contribution.Id))
            .Should().Be(3);
        (await dbContext.ContributionVotes.CountAsync(vote =>
            vote.ContributionId == contribution.Id && vote.UserId == changingVoter.User.Id))
            .Should().Be(1);
        (await dbContext.CommunityPois.CountAsync(poi => poi.Id == CommunityPoi.NewPointId(contribution.Id)))
            .Should().Be(0);
    }

    private static async Task<ContributionResponse> CreateContributionAsync(HttpClient authorClient, string slug)
    {
        var response = await authorClient.PostAsJsonAsync(
            $"/api/v1/zones/{slug}/contributions/points",
            new CreatePointContributionRequest(
                Category: "salud",
                Name: "Community Clinic",
                Schedule: "Monday-Friday 09:00-17:00",
                Address: "123 Integration Street",
                Latitude: -34.61,
                Longitude: -58.43,
                LivesOrWorksInZone: true));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var contribution = await response.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options);
        contribution.Should().NotBeNull();
        contribution!.Status.Should().Be(ContributionStatus.Pending);
        return contribution;
    }

    private static Task<HttpResponseMessage> VoteAsync(
        HttpClient voter,
        string slug,
        Guid contributionId,
        bool confirm) =>
        voter.PutAsJsonAsync(
            $"/api/v1/zones/{slug}/contributions/{contributionId}/validation",
            new VoteOnContributionRequest(confirm));

    private async Task CommunityPointCountAsync(Guid contributionId, int expectedCount)
    {
        await using var dbContext = database.CreateDbContext();
        (await dbContext.CommunityPois.CountAsync(poi => poi.Id == CommunityPoi.NewPointId(contributionId)))
            .Should().Be(expectedCount);
    }
}
