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
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class CommunityApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task CommunityPosts_WhenZoneIsSaved_PersistAndNotifyOtherSavedZoneUsers()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var subscriber = await TestApiUser.RegisterAsync(factory);

        foreach (var user in new[] { author, subscriber })
        {
            var saveZoneResponse = await user.Client.PutAsJsonAsync(
                $"/api/v1/saved-zones/{IntegrationZoneRepository.ZoneSlug}",
                new SaveZoneRequest { Name = "Integration Test Zone", Subtitle = "Community test" });
            saveZoneResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var questionResponse = await author.Client.PostAsJsonAsync(
            $"/api/v1/zones/{IntegrationZoneRepository.ZoneSlug}/questions",
            new CreateQuestionRequest("¿Cómo es la zona de noche?"));

        questionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var question = await questionResponse.Content.ReadFromJsonAsync<QuestionResponse>();
        question.Should().NotBeNull();
        question!.Text.Should().Be("¿Cómo es la zona de noche?");
        question.IsMine.Should().BeTrue();

        var reviewResponse = await author.Client.PutAsJsonAsync(
            $"/api/v1/zones/{IntegrationZoneRepository.ZoneSlug}/reviews/mine",
            new SaveReviewRequest(
                new ReviewAspectScoresRequest(4, 5, 4, 4, 3),
                "La zona es tranquila y tiene buen acceso al transporte.",
                YearsInZone: 5,
                Topics: ["transporte"]));

        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await reviewResponse.Content.ReadFromJsonAsync<ReviewResponse>();
        review.Should().NotBeNull();
        review!.Text.Should().Be("La zona es tranquila y tiene buen acceso al transporte.");
        review.IsMine.Should().BeTrue();

        var subscriberNotificationsResponse = await subscriber.Client.GetAsync("/api/v1/notifications");
        subscriberNotificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var subscriberNotifications = await subscriberNotificationsResponse.Content
            .ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        subscriberNotifications.Should().NotBeNull();
        subscriberNotifications!.Items.Should().ContainSingle(notification =>
            notification.Type == NotificationType.SavedZoneQuestion);
        subscriberNotifications.Items.Should().ContainSingle(notification =>
            notification.Type == NotificationType.SavedZoneReview);

        var authorNotificationsResponse = await author.Client.GetAsync("/api/v1/notifications");
        authorNotificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authorNotifications = await authorNotificationsResponse.Content
            .ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        authorNotifications.Should().NotBeNull();
        authorNotifications!.Items.Should().BeEmpty();

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.Questions.CountAsync(item => item.UserId == author.User.Id))
            .Should().Be(1);
        (await verificationContext.Reviews.CountAsync(item => item.UserId == author.User.Id))
            .Should().Be(1);
        (await verificationContext.Notifications.CountAsync(item => item.UserId == subscriber.User.Id))
            .Should().Be(2);
    }
}
