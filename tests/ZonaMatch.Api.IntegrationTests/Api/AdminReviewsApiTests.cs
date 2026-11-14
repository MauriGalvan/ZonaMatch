using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.DTOs.Review.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminReviewsApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task ListReviews_AfterApprovingAndRejecting_MovesBothToTheHistoryWithTheirDecision()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var approvedAuthor = await TestApiUser.RegisterAsync(factory);
        using var rejectedAuthor = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var zoneSlug = IntegrationZoneRepository.CreateCommunityZoneSlug();
        var approved = await SaveReviewAsync(approvedAuthor, zoneSlug, "Buena zona para vivir con chicos.");
        var rejected = await SaveReviewAsync(rejectedAuthor, zoneSlug, "Visiten mi página de alquileres.");

        var before = await ListAsync(administrator);
        before.Pending.Select(review => review.Id).Should().Contain([approved.Id, rejected.Id]);

        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, approved.Id, approve: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReviewModeration.ResolveAsync(administrator, zoneSlug, rejected.Id, approve: false, "Publicidad"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await ListAsync(administrator);
        after.Pending.Select(review => review.Id).Should().NotContain([approved.Id, rejected.Id]);

        var approvedItem = after.History.Should().ContainSingle(review => review.Id == approved.Id).Subject;
        approvedItem.Status.Should().Be("approved");
        approvedItem.AuthorEmail.Should().Be(approvedAuthor.User.Email);
        approvedItem.ResolvedByEmail.Should().Be(administrator.User.Email);
        approvedItem.ResolvedAt.Should().NotBeNull();

        var rejectedItem = after.History.Should().ContainSingle(review => review.Id == rejected.Id).Subject;
        rejectedItem.Status.Should().Be("rejected");
        rejectedItem.RejectionReason.Should().Be("Publicidad");
    }

    [Fact]
    public async Task ListReviews_WhenNotAdministrator_ReturnsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        (await member.Client.GetAsync("/api/v1/admin/reviews")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListReviews_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        (await client.GetAsync("/api/v1/admin/reviews")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<ReviewResponse> SaveReviewAsync(TestApiUser author, string zoneSlug, string text)
    {
        var response = await author.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/mine",
            new SaveReviewRequest(new ReviewAspectScoresRequest(4, 5, 4, 4, 3), text, YearsInZone: 2, Topics: ["transporte"]));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ReviewResponse>(ApiJson.Options))!;
    }

    private static async Task<AdminReviewListResponse> ListAsync(TestApiUser administrator)
    {
        var response = await administrator.Client.GetAsync("/api/v1/admin/reviews");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AdminReviewListResponse>(ApiJson.Options))!;
    }
}
