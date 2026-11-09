using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class ReviewDeletionApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task DeleteReview_WhenRequestedByOwner_RemovesReviewAndHelpfulVotesButCannotRemoveAnotherUsersReview()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var otherUser = await TestApiUser.RegisterAsync(factory);
        using var voter = await TestApiUser.RegisterAsync(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();

        var createReviewResponse = await author.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/mine",
            CreateReview());
        createReviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await createReviewResponse.Content.ReadFromJsonAsync<ReviewResponse>(ApiJson.Options);
        review.Should().NotBeNull();

        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, review!.Id, approve: true))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var helpfulVoteResponse = await voter.Client.PutAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/{review!.Id}/helpful-votes",
            content: null);
        helpfulVoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var otherUserDeleteResponse = await otherUser.Client.DeleteAsync($"/api/v1/zones/{zoneSlug}/reviews/mine");
        otherUserDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reviewBeforeDeletionResponse = await otherUser.Client.GetAsync($"/api/v1/zones/{zoneSlug}/reviews");
        reviewBeforeDeletionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviewsBeforeDeletion = await reviewBeforeDeletionResponse.Content.ReadFromJsonAsync<ZoneReviewListResponse>(ApiJson.Options);
        reviewsBeforeDeletion.Should().NotBeNull();
        reviewsBeforeDeletion!.Reviews.Should().ContainSingle(item => item.Id == review.Id && item.HelpfulCount == 1);

        var deleteResponse = await author.Client.DeleteAsync($"/api/v1/zones/{zoneSlug}/reviews/mine");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reviewsAfterDeletionResponse = await voter.Client.GetAsync($"/api/v1/zones/{zoneSlug}/reviews");
        reviewsAfterDeletionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviewsAfterDeletion = await reviewsAfterDeletionResponse.Content.ReadFromJsonAsync<ZoneReviewListResponse>(ApiJson.Options);
        reviewsAfterDeletion.Should().NotBeNull();
        reviewsAfterDeletion!.Reviews.Should().BeEmpty();

        var repeatedDeleteResponse = await author.Client.DeleteAsync($"/api/v1/zones/{zoneSlug}/reviews/mine");
        repeatedDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.Reviews.AnyAsync(item => item.Id == review.Id)).Should().BeFalse();
        (await verificationContext.ReviewHelpfulVotes.AnyAsync(item => item.ReviewId == review.Id)).Should().BeFalse();
    }

    private static SaveReviewRequest CreateReview() => new(
        new ReviewAspectScoresRequest(4, 5, 4, 4, 3),
        "La zona tiene buenos servicios y conexiones.",
        YearsInZone: 5,
        Topics: ["transporte"]);
}
