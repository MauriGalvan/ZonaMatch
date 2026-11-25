using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Nps;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class NpsApiTests(PostgreSqlDatabaseFixture database)
{
    private const string Endpoint = "/api/v1/nps";
    private const string Feedback = "/api/v1/analysis-follow-ups";

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private WebApplicationFactory<Program> Factory(Clock clock) => new ZonaMatchApiFactory(database.ConnectionString)
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));

    [Fact]
    public async Task Invitations_RequireSevenDaysAndTwoDistinctSuccessfulAnalyses()
    {
        var clock = new Clock();
        using var factory = Factory(clock);
        using var owner = await TestApiUser.RegisterAsync(factory);
        await AgeAccountAsync(owner.User.Id, clock.Now.AddDays(-6));
        (await InviteAsync(owner.Client)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ScoreAsync(owner.Client, "800000");
        await ScoreAsync(owner.Client, "800000");
        await using (var db = database.CreateDbContext())
            (await db.CompletedZoneAnalyses.CountAsync(item => item.UserId == owner.User.Id)).Should().Be(1);
        clock.Now = clock.Now.AddDays(1);
        (await InviteAsync(owner.Client)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.PostAsJsonAsync("/api/v1/scores", new { maximumRent = "", criteria = new { seguridad = "alta" } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ScoreAsync(owner.Client, "900000");
        (await InviteAsync(owner.Client)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Answer_AllowsZeroAndRestoresTheSameInvitationAcrossSessionsAndOptOut()
    {
        var clock = new Clock();
        using var factory = Factory(clock);
        using var owner = await EligibleAsync(factory, clock);
        using var other = await TestApiUser.RegisterAsync(factory);
        var invitation = await InvitationAsync(owner.Client);
        using var secondDevice = factory.CreateClient();
        secondDevice.DefaultRequestHeaders.Authorization = owner.Client.DefaultRequestHeaders.Authorization;
        (await InvitationAsync(secondDevice)).Id.Should().Be(invitation.Id);
        (await other.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { score = 10 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { score = 11 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.PutAsJsonAsync($"{Feedback}/preferences", new { surveysEnabled = false })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await InviteAsync(secondDevice)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await secondDevice.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { score = 0 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await owner.Client.PutAsJsonAsync($"{Feedback}/preferences", new { surveysEnabled = true });
        (await InvitationAsync(secondDevice)).Id.Should().Be(invitation.Id);
        (await secondDevice.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { score = 0, comment = "  No me ayudó.  " })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { score = 10 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var db = database.CreateDbContext();
        var saved = await db.NpsSurveys.SingleAsync(item => item.Id == invitation.Id);
        saved.Score.Should().Be(0);
        saved.Comment.Should().Be("No me ayudó.");
        (await db.UserFeedbackPreferences.SingleAsync(item => item.UserId == owner.User.Id)).NextInvitationAt.Should().Be(clock.Now.AddDays(14));
    }

    [Theory]
    [InlineData(false, 90)]
    [InlineData(true, 30)]
    public async Task Invitations_RespectResponseAndPostponementCooldowns(bool postpone, int days)
    {
        var clock = new Clock();
        using var factory = Factory(clock);
        using var owner = await EligibleAsync(factory, clock);
        var invitation = await InvitationAsync(owner.Client);
        if (postpone)
            (await owner.Client.PostAsync($"{Endpoint}/{invitation.Id}/postponements", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        else
            (await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { score = 10 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        clock.Now = clock.Now.AddDays(days).AddSeconds(-1);
        (await InviteAsync(owner.Client)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        clock.Now = clock.Now.AddSeconds(1);
        (await InvitationAsync(owner.Client)).Id.Should().NotBe(invitation.Id);
    }

    [Fact]
    public async Task Invitations_PrioritizeDueFollowUpsAndKeepOnlyOneSurveyActive()
    {
        var clock = new Clock();
        using var factory = Factory(clock);
        using var owner = await EligibleAsync(factory, clock);
        await AddAnalysisAsync(owner.User.Id, clock.Now.AddDays(-20));
        (await InviteAsync(owner.Client)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.PostAsync($"{Feedback}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await InviteAsync(owner.Client)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FollowUp_WaitsFourteenDaysAfterNpsAndCannotReplaceAnActiveNps()
    {
        var clock = new Clock();
        using var factory = Factory(clock);
        using var owner = await EligibleAsync(factory, clock);
        var invitation = await InvitationAsync(owner.Client);
        await AddAnalysisAsync(owner.User.Id, clock.Now.AddDays(-20));
        (await owner.Client.PostAsync($"{Feedback}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await owner.Client.PostAsJsonAsync($"{Endpoint}/{invitation.Id}/responses", new { score = 8 });
        clock.Now = clock.Now.AddDays(14).AddSeconds(-1);
        (await owner.Client.PostAsync($"{Feedback}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        clock.Now = clock.Now.AddSeconds(1);
        (await owner.Client.PostAsync($"{Feedback}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SimultaneousInvitations_ReserveOneCard()
    {
        var clock = new Clock();
        using var factory = Factory(clock);
        using var owner = await EligibleAsync(factory, clock);
        var responses = await Task.WhenAll(InviteAsync(owner.Client), InviteAsync(owner.Client));
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict);
        responses.Should().Contain(response => response.StatusCode == HttpStatusCode.OK);
        await using var db = database.CreateDbContext();
        (await db.NpsSurveys.CountAsync(item => item.UserId == owner.User.Id && item.CompletedAt == null && item.PostponedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task Admin_ListsOnlyAnswersAndComputesNpsAcrossAllPages()
    {
        var clock = new Clock();
        using var factory = Factory(clock);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var anonymous = factory.CreateClient();
        const string adminEndpoint = "/api/v1/admin/nps";
        (await anonymous.GetAsync(adminEndpoint)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await owner.Client.GetAsync(adminEndpoint)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.PostAsync($"{Endpoint}/invitations", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        owner.AuthenticateAsAdministrator(factory);
        var start = new DateTimeOffset(2050, 5, 5, 3, 0, 0, TimeSpan.Zero);
        await using (var db = database.CreateDbContext())
        {
            var scores = new[] { 0, 7, 9, 10 }.Concat(Enumerable.Repeat(10, 21)).ToArray();
            db.NpsSurveys.AddRange(scores.Select((score, index) => new NpsSurvey { UserId = owner.User.Id, InvitedAt = start, CompletedAt = start.AddMinutes(index), Score = score, Comment = index == 0 ? "Motivo" : null }));
            db.NpsSurveys.Add(new NpsSurvey { UserId = owner.User.Id, InvitedAt = start });
            db.NpsSurveys.Add(new NpsSurvey { UserId = owner.User.Id, InvitedAt = start, CompletedAt = start.AddSeconds(-1), Score = 0 });
            db.NpsSurveys.Add(new NpsSurvey { UserId = owner.User.Id, InvitedAt = start, CompletedAt = start.AddDays(1), Score = 0 });
            await db.SaveChangesAsync();
        }
        const string range = "?from=2050-05-05&to=2050-05-05";
        var first = (await owner.Client.GetFromJsonAsync<AdminNpsResponse>(adminEndpoint + range))!;
        first.Summary.Should().Be(new NpsSummary(25, 23, 1, 1, 88));
        first.Items.Should().HaveCount(20);
        var second = (await owner.Client.GetFromJsonAsync<AdminNpsResponse>(adminEndpoint + range + "&page=2"))!;
        second.Items.Should().HaveCount(5);
        second.Summary.Should().Be(first.Summary);
        var zero = (await owner.Client.GetFromJsonAsync<AdminNpsResponse>(adminEndpoint + range + "&score=0"))!;
        zero.Items.Should().ContainSingle().Which.Comment.Should().Be("Motivo");
        zero.Summary.Nps.Should().Be(-100);
        var empty = (await owner.Client.GetFromJsonAsync<AdminNpsResponse>(adminEndpoint + range + "&score=5"))!;
        empty.Summary.Nps.Should().BeNull();
        empty.Items.Should().BeEmpty();
        (await owner.Client.GetAsync(adminEndpoint + "?from=2050-05-06&to=2050-05-05")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.Client.GetAsync(adminEndpoint + "?score=11")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client) => client.PostAsync($"{Endpoint}/invitations", null);
    private static async Task<NpsInvitationResponse> InvitationAsync(HttpClient client)
    {
        var response = await InviteAsync(client);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<NpsInvitationResponse>())!;
    }

    private async Task<TestApiUser> EligibleAsync(WebApplicationFactory<Program> factory, Clock clock)
    {
        var user = await TestApiUser.RegisterAsync(factory);
        await AgeAccountAsync(user.User.Id, clock.Now.AddDays(-8));
        await ScoreAsync(user.Client, "800000");
        await ScoreAsync(user.Client, "900000");
        return user;
    }

    private static async Task ScoreAsync(HttpClient client, string rent)
    {
        var response = await client.PostAsJsonAsync("/api/v1/scores", new { maximumRent = rent, criteria = new { seguridad = "alta" }, addedZones = new[] { "safety-zone" } });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task AgeAccountAsync(Guid id, DateTimeOffset createdAt)
    {
        await using var db = database.CreateDbContext();
        (await db.Users.SingleAsync(item => item.Id == id)).CreatedAt = createdAt;
        await db.SaveChangesAsync();
    }

    private async Task AddAnalysisAsync(Guid userId, DateTimeOffset createdAt)
    {
        await using var db = database.CreateDbContext();
        var criteria = new Context { UserId = userId, Type = ContextType.Analysis, PeopleCount = 1, CreatedAt = createdAt, UpdatedAt = createdAt };
        db.Analyses.Add(new Analysis { UserId = userId, Name = "Seguimiento pendiente", ContextId = criteria.Id, Context = criteria, CreatedAt = createdAt, UpdatedAt = createdAt });
        await db.SaveChangesAsync();
    }
}
