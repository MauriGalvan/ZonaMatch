using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Notification.Responses;
using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.DTOs.SavedZone.Requests;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class NotificationApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task Notifications_WhenReadOrReadAll_AreScopedToAuthenticatedUser()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var subscriber = await TestApiUser.RegisterAsync(factory);
        using var firstAuthor = await TestApiUser.RegisterAsync(factory);
        using var secondAuthor = await TestApiUser.RegisterAsync(factory);
        using var unrelatedUser = await TestApiUser.RegisterAsync(factory);

        var saveZoneResponse = await subscriber.Client.PutAsJsonAsync(
            $"/api/v1/saved-zones/{IntegrationZoneRepository.ZoneSlug}",
            new SaveZoneRequest { Name = "Integration Test Zone", Subtitle = "Notifications" });
        saveZoneResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var author in new[] { firstAuthor, secondAuthor })
        {
            var questionResponse = await author.Client.PostAsJsonAsync(
                $"/api/v1/zones/{IntegrationZoneRepository.ZoneSlug}/questions",
                new CreateQuestionRequest("¿Qué servicios hay cerca de la plaza?"));
            questionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var initialResponse = await subscriber.Client.GetAsync("/api/v1/notifications");
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialNotifications = await initialResponse.Content.ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        initialNotifications.Should().NotBeNull();
        initialNotifications!.UnreadCount.Should().Be(2);
        initialNotifications.Items.Should().HaveCount(2);

        var notificationId = initialNotifications.Items[0].Id;
        var otherUserReadResponse = await unrelatedUser.Client.PatchAsync(
            $"/api/v1/notifications/{notificationId}/read",
            content: null);
        otherUserReadResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownReadResponse = await subscriber.Client.PatchAsync(
            $"/api/v1/notifications/{notificationId}/read",
            content: null);
        ownReadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterSingleReadResponse = await subscriber.Client.GetAsync("/api/v1/notifications");
        var afterSingleRead = await afterSingleReadResponse.Content.ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        afterSingleRead.Should().NotBeNull();
        afterSingleRead!.UnreadCount.Should().Be(1);
        afterSingleRead.Items.Single(item => item.Id == notificationId).ReadAt.Should().NotBeNull();

        var unrelatedReadAllResponse = await unrelatedUser.Client.PatchAsync(
            "/api/v1/notifications/read-all",
            content: null);
        unrelatedReadAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUnrelatedReadAllResponse = await subscriber.Client.GetAsync("/api/v1/notifications");
        var afterUnrelatedReadAll = await afterUnrelatedReadAllResponse.Content
            .ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        afterUnrelatedReadAll.Should().NotBeNull();
        afterUnrelatedReadAll!.UnreadCount.Should().Be(1);

        var ownReadAllResponse = await subscriber.Client.PatchAsync(
            "/api/v1/notifications/read-all",
            content: null);
        ownReadAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var finalResponse = await subscriber.Client.GetAsync("/api/v1/notifications");
        var finalNotifications = await finalResponse.Content.ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        finalNotifications.Should().NotBeNull();
        finalNotifications!.UnreadCount.Should().Be(0);
        finalNotifications.Items.Should().OnlyContain(item => item.ReadAt.HasValue);
    }
}
