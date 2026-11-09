using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Notification.Responses;
using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.DTOs.SavedZone.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class ReviewModerationApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task Review_WhenPending_IsOnlyVisibleToItsAuthorUntilAnAdministratorApprovesIt()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var subscriber = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        using var anonymous = CreateAnonymousClient(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();

        var saveZoneResponse = await subscriber.Client.PutAsJsonAsync(
            $"/api/v1/saved-zones/{zoneSlug}",
            new SaveZoneRequest { Name = "Moderation zone", Subtitle = "Review moderation" });
        saveZoneResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // The administrators lookup reads the database, not the token.
        await using (var context = database.CreateDbContext())
        {
            await context.Users
                .Where(user => user.Id == administrator.User.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsAdministrator, true));
        }

        var review = await SaveReviewAsync(author, zoneSlug, "La zona es tranquila y tiene buen acceso al transporte.");
        review.Status.Should().Be(ReviewStatus.Pending);

        var emailService = GetEmailService(factory);
        emailService.SentEmails.Should().ContainSingle(email =>
            email.To == author.User.Email && email.Subject == "Tu reseña en ZonaMatch está pendiente de aprobación");
        emailService.SentEmails.Should().ContainSingle(email =>
            email.To == administrator.User.Email && email.Subject == "Hay una reseña para revisar en ZonaMatch");

        var forAuthor = await ListReviewsAsync(author.Client, zoneSlug);
        forAuthor.Reviews.Should().ContainSingle(item => item.Id == review.Id && item.IsMine && item.Status == ReviewStatus.Pending);
        forAuthor.Summary.Total.Should().Be(0);

        foreach (var client in new[] { subscriber.Client, anonymous })
        {
            var hidden = await ListReviewsAsync(client, zoneSlug);
            hidden.Reviews.Should().BeEmpty();
            hidden.Summary.Total.Should().Be(0);
        }

        (await GetNotificationsAsync(subscriber)).Should().NotContain(item => item.Type == NotificationType.SavedZoneReview);

        // The administrator sees it in the bell and, unlike other users, among the zone's reviews to moderate it.
        (await GetNotificationsAsync(administrator)).Should().ContainSingle(item =>
            item.Type == NotificationType.ReviewPendingModeration && item.Link.Contains(zoneSlug));
        (await GetNotificationsAsync(author)).Should().NotContain(item => item.Type == NotificationType.ReviewPendingModeration);
        (await ListReviewsAsync(administrator.Client, zoneSlug)).Reviews
            .Should().ContainSingle(item => item.Id == review.Id && !item.IsMine && item.Status == ReviewStatus.Pending);

        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, review.Id, approve: true))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        foreach (var client in new[] { subscriber.Client, anonymous })
        {
            var visible = await ListReviewsAsync(client, zoneSlug);
            visible.Reviews.Should().ContainSingle(item => item.Id == review.Id && item.Status == ReviewStatus.Approved && !item.IsMine);
            visible.Summary.Total.Should().Be(1);
        }

        (await GetNotificationsAsync(subscriber)).Should().ContainSingle(item => item.Type == NotificationType.SavedZoneReview);
        (await GetNotificationsAsync(administrator)).Should().ContainSingle(item =>
            item.Type == NotificationType.ReviewPendingModeration && item.ReadAt != null);
        emailService.SentEmails.Should().ContainSingle(email =>
            email.To == author.User.Email && email.Subject == "Tu reseña en ZonaMatch fue aprobada");

        await using var verificationContext = database.CreateDbContext();
        var stored = await verificationContext.Reviews.SingleAsync(item => item.Id == review.Id);
        stored.Status.Should().Be(ReviewStatus.Approved);
        stored.ResolvedByUserId.Should().Be(administrator.User.Id);
        stored.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Review_WhenRejectedOrEdited_ReturnsToThePendingQueueAfterTheAuthorFixesIt()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var otherUser = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();

        var emailService = GetEmailService(factory);
        var review = await SaveReviewAsync(author, zoneSlug, "La zona es tranquila y tiene buen acceso al transporte.");

        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, review.Id, approve: false, reason: null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, review.Id, approve: false, reason: "Incluye datos personales."))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, review.Id, approve: true))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        emailService.SentEmails.Should().ContainSingle(email =>
            email.To == author.User.Email
            && email.Subject == "Tu reseña en ZonaMatch fue rechazada"
            && email.Body.Contains("Incluye datos personales."));

        var rejected = (await ListReviewsAsync(author.Client, zoneSlug)).Reviews.Should().ContainSingle().Subject;
        rejected.Status.Should().Be(ReviewStatus.Rejected);
        rejected.RejectionReason.Should().Be("Incluye datos personales.");
        (await ListReviewsAsync(otherUser.Client, zoneSlug)).Reviews.Should().BeEmpty();
        (await ListPendingAsync(administrator)).Should().NotContain(item => item.Review.Id == review.Id);

        var resubmitted = await SaveReviewAsync(author, zoneSlug, "Corregí la reseña y quité los datos personales.");
        resubmitted.Id.Should().Be(review.Id);
        resubmitted.Status.Should().Be(ReviewStatus.Pending);
        resubmitted.RejectionReason.Should().BeNull();

        var pending = await ListPendingAsync(administrator);
        pending.Should().ContainSingle(item => item.Review.Id == review.Id && item.ZoneSlug == zoneSlug);

        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, review.Id, approve: true))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListPendingAsync(administrator)).Should().NotContain(item => item.Review.Id == review.Id);
        (await ListReviewsAsync(otherUser.Client, zoneSlug)).Reviews.Should().ContainSingle(item => item.Id == review.Id);

        // An approved review that is edited disappears for everybody else until it is approved again.
        (await SaveReviewAsync(author, zoneSlug, "Actualicé mi opinión después de mudarme a la zona.")).Status
            .Should().Be(ReviewStatus.Pending);
        (await ListReviewsAsync(otherUser.Client, zoneSlug)).Reviews.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewModeration_WhenCalledWithoutAdministratorRole_IsRejected()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        using var anonymous = CreateAnonymousClient(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();

        var review = await SaveReviewAsync(author, zoneSlug, "La zona es tranquila y tiene buen acceso al transporte.");
        var resolutionPath = $"/api/v1/zones/{zoneSlug}/reviews/{review.Id}/resolution";
        var body = new ResolveReviewRequest(Approve: true);

        (await author.Client.PutAsJsonAsync(resolutionPath, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await author.Client.GetAsync("/api/v1/reviews/pending")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.PutAsJsonAsync(resolutionPath, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/v1/reviews/pending")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, Guid.NewGuid(), approve: true))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.Reviews.SingleAsync(item => item.Id == review.Id)).Status.Should().Be(ReviewStatus.Pending);
    }

    private static IntegrationEmailService GetEmailService(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IEmailService>().Should().BeOfType<IntegrationEmailService>().Subject;

    private static HttpClient CreateAnonymousClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<ReviewResponse> SaveReviewAsync(TestApiUser user, string zoneSlug, string text)
    {
        var response = await user.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/mine",
            new SaveReviewRequest(
                new ReviewAspectScoresRequest(4, 5, 4, 4, 3),
                text,
                YearsInZone: 5,
                Topics: ["transporte"]));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var review = await response.Content.ReadFromJsonAsync<ReviewResponse>(ApiJson.Options);
        review.Should().NotBeNull();
        return review!;
    }

    private static async Task<ZoneReviewListResponse> ListReviewsAsync(HttpClient client, string zoneSlug)
    {
        var response = await client.GetAsync($"/api/v1/zones/{zoneSlug}/reviews");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviews = await response.Content.ReadFromJsonAsync<ZoneReviewListResponse>(ApiJson.Options);
        reviews.Should().NotBeNull();
        return reviews!;
    }

    private static async Task<IReadOnlyList<PendingReviewResponse>> ListPendingAsync(TestApiUser administrator)
    {
        var response = await administrator.Client.GetAsync("/api/v1/reviews/pending");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pending = await response.Content.ReadFromJsonAsync<IReadOnlyList<PendingReviewResponse>>(ApiJson.Options);
        pending.Should().NotBeNull();
        return pending!;
    }

    private static async Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(TestApiUser user)
    {
        var response = await user.Client.GetAsync("/api/v1/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var notifications = await response.Content.ReadFromJsonAsync<NotificationListResponse>(ApiJson.Options);
        notifications.Should().NotBeNull();
        return notifications!.Items;
    }
}
