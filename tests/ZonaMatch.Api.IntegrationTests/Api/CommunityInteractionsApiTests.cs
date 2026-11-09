using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Notification.Responses;
using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.DTOs.Question.Responses;
using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.DTOs.SavedZone.Requests;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class CommunityInteractionsApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task AnswerQuestion_WhenPostedByAnotherUser_PersistsAnswerAndNotifiesQuestionAuthor()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var answerer = await TestApiUser.RegisterAsync(factory);
        using var subscriber = await TestApiUser.RegisterAsync(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();

        var saveZoneResponse = await subscriber.Client.PutAsJsonAsync(
            $"/api/v1/saved-zones/{zoneSlug}",
            new SaveZoneRequest { Name = "Community question zone", Subtitle = "Question notification" });
        saveZoneResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createQuestionResponse = await author.Client.PostAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/questions",
            new CreateQuestionRequest("¿Qué servicios hay cerca de la plaza?"));
        createQuestionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var question = await createQuestionResponse.Content.ReadFromJsonAsync<QuestionResponse>();
        question.Should().NotBeNull();

        var answerResponse = await answerer.Client.PostAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/questions/{question!.Id}/answers",
            new CreateAnswerRequest("Hay comercios y varias líneas de colectivo."));
        answerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var answer = await answerResponse.Content.ReadFromJsonAsync<AnswerResponse>();
        answer.Should().NotBeNull();
        answer!.Text.Should().Be("Hay comercios y varias líneas de colectivo.");
        answer.IsMine.Should().BeTrue();

        var questionsResponse = await author.Client.GetAsync($"/api/v1/zones/{zoneSlug}/questions");
        questionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var questions = await questionsResponse.Content.ReadFromJsonAsync<IReadOnlyList<QuestionResponse>>();
        questions.Should().ContainSingle();
        questions![0].Answers.Should().ContainSingle(savedAnswer =>
            savedAnswer.Id == answer.Id
            && savedAnswer.Text == answer.Text
            && !savedAnswer.IsMine);

        var authorNotificationsResponse = await author.Client.GetAsync("/api/v1/notifications");
        authorNotificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authorNotifications = await authorNotificationsResponse.Content
            .ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        authorNotifications.Should().NotBeNull();
        authorNotifications!.Items.Should().ContainSingle(notification =>
            notification.Type == NotificationType.CommunityReply);

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.Answers.CountAsync(item => item.QuestionId == question.Id)).Should().Be(1);
        (await verificationContext.Notifications.CountAsync(item => item.UserId == author.User.Id)).Should().Be(1);
    }

    [Fact]
    public async Task HelpfulReviewVote_WhenRepeatedAndRemoved_IsIdempotentAndCannotBeCastOnOwnReview()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var voter = await TestApiUser.RegisterAsync(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();

        var createReviewResponse = await author.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/mine",
            CreateReview("La zona tiene buenos servicios y conexiones."));
        createReviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await createReviewResponse.Content.ReadFromJsonAsync<ReviewResponse>(ApiJson.Options);
        review.Should().NotBeNull();

        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, review!.Id, approve: true))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var selfVoteResponse = await author.Client.PutAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/{review!.Id}/helpful-votes",
            content: null);
        selfVoteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var firstVoteResponse = await voter.Client.PutAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/{review.Id}/helpful-votes",
            content: null);
        firstVoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var repeatedVoteResponse = await voter.Client.PutAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/{review.Id}/helpful-votes",
            content: null);
        repeatedVoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var votedReviewsResponse = await voter.Client.GetAsync($"/api/v1/zones/{zoneSlug}/reviews");
        votedReviewsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var votedReviews = await votedReviewsResponse.Content.ReadFromJsonAsync<ZoneReviewListResponse>(ApiJson.Options);
        votedReviews.Should().NotBeNull();
        votedReviews!.Reviews.Should().ContainSingle(item =>
            item.Id == review.Id && item.HelpfulCount == 1 && item.MarkedHelpfulByMe && !item.IsMine);

        var firstUnvoteResponse = await voter.Client.DeleteAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/{review.Id}/helpful-votes");
        firstUnvoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var repeatedUnvoteResponse = await voter.Client.DeleteAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/{review.Id}/helpful-votes");
        repeatedUnvoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unvotedReviewsResponse = await voter.Client.GetAsync($"/api/v1/zones/{zoneSlug}/reviews");
        var unvotedReviews = await unvotedReviewsResponse.Content.ReadFromJsonAsync<ZoneReviewListResponse>(ApiJson.Options);
        unvotedReviews.Should().NotBeNull();
        unvotedReviews!.Reviews.Should().ContainSingle(item =>
            item.Id == review.Id && item.HelpfulCount == 0 && !item.MarkedHelpfulByMe);

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.ReviewHelpfulVotes.CountAsync(item => item.ReviewId == review.Id)).Should().Be(0);
    }

    [Fact]
    public async Task SaveReview_WhenUpdatingExistingReview_UpdatesSameRecordWithoutDuplicateNotifications()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var subscriber = await TestApiUser.RegisterAsync(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();

        foreach (var user in new[] { author, subscriber })
        {
            var saveZoneResponse = await user.Client.PutAsJsonAsync(
                $"/api/v1/saved-zones/{zoneSlug}",
                new SaveZoneRequest { Name = "Review update zone", Subtitle = "Review notification" });
            saveZoneResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var firstSaveResponse = await author.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/mine",
            CreateReview("La zona tiene buenos servicios y conexiones."));
        firstSaveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstReview = await firstSaveResponse.Content.ReadFromJsonAsync<ReviewResponse>(ApiJson.Options);
        firstReview.Should().NotBeNull();

        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, firstReview!.Id, approve: true))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updateResponse = await author.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/mine",
            CreateReview("Actualicé mi opinión sobre los servicios y conexiones."));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedReview = await updateResponse.Content.ReadFromJsonAsync<ReviewResponse>(ApiJson.Options);
        updatedReview.Should().NotBeNull();
        updatedReview!.Id.Should().Be(firstReview!.Id);
        updatedReview.Text.Should().Be("Actualicé mi opinión sobre los servicios y conexiones.");

        // Changing the text sends the review back to pending; approving it again must not notify the subscriber twice.
        updatedReview.Status.Should().Be(ReviewStatus.Pending);
        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, updatedReview.Id, approve: true))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reviewsResponse = await subscriber.Client.GetAsync($"/api/v1/zones/{zoneSlug}/reviews");
        reviewsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviews = await reviewsResponse.Content.ReadFromJsonAsync<ZoneReviewListResponse>(ApiJson.Options);
        reviews.Should().NotBeNull();
        reviews!.Reviews.Should().ContainSingle(item => item.Id == firstReview.Id && item.Text == updatedReview.Text);

        var notificationsResponse = await subscriber.Client.GetAsync("/api/v1/notifications");
        notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        notifications.Should().NotBeNull();
        notifications!.Items.Should().ContainSingle(item => item.Type == NotificationType.SavedZoneReview);

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.Reviews.CountAsync(item => item.ZoneSlug == zoneSlug && item.UserId == author.User.Id))
            .Should().Be(1);
        (await verificationContext.Notifications.CountAsync(item => item.UserId == subscriber.User.Id))
            .Should().Be(1);
    }

    private static SaveReviewRequest CreateReview(string text) => new(
        new ReviewAspectScoresRequest(4, 5, 4, 4, 3),
        text,
        YearsInZone: 5,
        Topics: ["transporte"]);
}
