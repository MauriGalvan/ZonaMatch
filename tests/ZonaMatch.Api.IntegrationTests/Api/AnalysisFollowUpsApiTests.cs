using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.AnalysisFollowUp.Requests;
using ZonaMatch.Application.DTOs.AnalysisFollowUp.Responses;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AnalysisFollowUpsApiTests(PostgreSqlDatabaseFixture database)
{
    private const string Endpoint = "/api/v1/analysis-follow-ups";

    [Fact]
    public async Task FollowUps_RequireAuthentication()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        (await client.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"{Endpoint}/preferences")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invitation_UsesAnOldOriginalAndReturnsTheSameCardOnAnotherDevice()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        (await owner.Client.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var recent = await AddAnalysisAsync(owner.User.Id, 13);
        await AddAnalysisAsync(owner.User.Id, 40, recent.Id);
        (await owner.Client.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var original = await AddAnalysisAsync(owner.User.Id, 20);
        var first = await InviteAsync(owner.Client);
        first.AnalysisId.Should().Be(original.Id);

        using var otherDevice = factory.CreateClient();
        otherDevice.DefaultRequestHeaders.Authorization = owner.Client.DefaultRequestHeaders.Authorization;
        (await InviteAsync(otherDevice)).Id.Should().Be(first.Id);
        await using var db = database.CreateDbContext();
        (await db.AnalysisFollowUps.CountAsync(f => f.UserId == owner.User.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Response_IsValidatedOwnedPersistedAndCannotBeOverwritten()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var otherUser = await TestApiUser.RegisterAsync(factory);
        await AddAnalysisAsync(owner.User.Id, 20);
        await AddAnalysisAsync(owner.User.Id, 18);
        var invitation = await InviteAsync(owner.Client);
        var answer = new SubmitAnalysisFollowUpRequest { Outcome = "chose-recommended-zone", Helpfulness = 5, Comment = "  Helpful result  " };
        (await otherUser.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", answer)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherUser.Client.PostAsync($"{Endpoint}/{invitation.Id}/postponements", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { outcome = "invalid", helpfulness = 5 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { outcome = "still-searching", helpfulness = 6 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { outcome = "still-searching" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", answer)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", answer)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = database.CreateDbContext();
        var saved = await db.AnalysisFollowUps.SingleAsync(f => f.Id == invitation.Id);
        saved.Outcome.Should().Be(answer.Outcome);
        saved.Helpfulness.Should().Be(5);
        saved.Comment.Should().Be("Helpful result");
        saved.CompletedAt.Should().NotBeNull();
        (await db.UserFeedbackPreferences.SingleAsync(p => p.UserId == owner.User.Id)).NextInvitationAt
            .Should().BeAfter(DateTimeOffset.UtcNow.AddDays(13));
    }

    [Fact]
    public async Task PostponementAndOptOut_PersistAcrossSessionsAndReenablingKeepsTheWaitingPeriod()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        await AddAnalysisAsync(owner.User.Id, 20);
        await AddAnalysisAsync(owner.User.Id, 18);
        var invitation = await InviteAsync(owner.Client);
        (await owner.Client.PostAsync($"{Endpoint}/{invitation.Id}/postponements", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.PutAsJsonAsync($"{Endpoint}/preferences", new { surveysEnabled = false })).StatusCode.Should().Be(HttpStatusCode.OK);

        using var otherDevice = factory.CreateClient();
        otherDevice.DefaultRequestHeaders.Authorization = owner.Client.DefaultRequestHeaders.Authorization;
        (await otherDevice.GetFromJsonAsync<FeedbackPreferencesResponse>($"{Endpoint}/preferences"))!.SurveysEnabled.Should().BeFalse();
        (await otherDevice.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await otherDevice.PutAsJsonAsync($"{Endpoint}/preferences", new { surveysEnabled = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await otherDevice.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = database.CreateDbContext();
        (await db.AnalysisFollowUps.SingleAsync(f => f.Id == invitation.Id)).PostponedUntil
            .Should().BeAfter(DateTimeOffset.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task ConcurrentInvitationsAndAnswers_DoNotDuplicateOrOverwriteFeedback()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        await AddAnalysisAsync(owner.User.Id, 20);
        var invitations = await Task.WhenAll(
            owner.Client.PostAsync($"{Endpoint}/invitations", null),
            owner.Client.PostAsync($"{Endpoint}/invitations", null));
        invitations.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict);
        invitations.Should().Contain(response => response.StatusCode == HttpStatusCode.OK);
        var invitation = await InviteAsync(owner.Client);
        var answer = new SubmitAnalysisFollowUpRequest { Outcome = "still-searching", Helpfulness = 3 };
        var responses = await Task.WhenAll(
            owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", answer),
            owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", answer));
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NoContent);
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Conflict);
        await using var db = database.CreateDbContext();
        (await db.AnalysisFollowUps.CountAsync(f => f.UserId == owner.User.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentPreferences_DoNotUndoAnOptOutOrANewerWaitingPeriod(bool optOut)
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        await AddAnalysisAsync(owner.User.Id, 20);
        await InviteAsync(owner.Client);
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstRepository = new AnalysisFollowUpRepository(firstContext);
        var secondRepository = new AnalysisFollowUpRepository(secondContext);
        var first = (await firstRepository.GetPreferencesAsync(owner.User.Id))!;
        var stale = (await secondRepository.GetPreferencesAsync(owner.User.Id))!;
        if (optOut)
            first.SurveysEnabled = false;
        else
            first.NextInvitationAt = DateTimeOffset.UtcNow.AddDays(30);
        await firstRepository.SaveChangesAsync();

        if (optOut)
            stale.NextInvitationAt = DateTimeOffset.UtcNow.AddDays(14);
        else
            stale.SurveysEnabled = false;
        await Assert.ThrowsAsync<ConflictException>(() => secondRepository.SaveChangesAsync());
        await using var verification = database.CreateDbContext();
        var saved = await verification.UserFeedbackPreferences.SingleAsync(p => p.UserId == owner.User.Id);
        if (optOut) saved.SurveysEnabled.Should().BeFalse();
        else saved.NextInvitationAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(29));
    }

    private async Task<Analysis> AddAnalysisAsync(Guid userId, int ageDays, Guid? originalId = null)
    {
        await using var db = database.CreateDbContext();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-ageDays);
        var context = new Context { UserId = userId, Type = ContextType.Analysis, PeopleCount = 1, CreatedAt = createdAt, UpdatedAt = createdAt };
        var analysis = new Analysis { UserId = userId, Name = "Follow-up test", Context = context, ContextId = context.Id,
            CreatedAt = createdAt, UpdatedAt = createdAt, OriginalAnalysisId = originalId };
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync();
        return analysis;
    }

    private static async Task<AnalysisFollowUpResponse> InviteAsync(HttpClient client)
    {
        var response = await client.PostAsync($"{Endpoint}/invitations", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AnalysisFollowUpResponse>())!;
    }
}
