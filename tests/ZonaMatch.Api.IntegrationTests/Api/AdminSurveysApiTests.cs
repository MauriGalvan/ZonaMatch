using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminSurveysApiTests(PostgreSqlDatabaseFixture database)
{
    private const string Endpoint = "/api/v1/admin/surveys";

    [Fact]
    public async Task List_RequiresAnAdministrator()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var anonymous = factory.CreateClient();
        using var member = await TestApiUser.RegisterAsync(factory);
        (await anonymous.GetAsync(Endpoint)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await member.Client.GetAsync(Endpoint)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_FiltersArgentinaDaysAndSummarizesEveryPageWithoutPendingInvitations()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var admin = await TestApiUser.RegisterAsync(factory);
        admin.AuthenticateAsAdministrator(factory);
        var start = new DateTimeOffset(2040, 5, 5, 3, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < 21; index++)
            await AddAnswerAsync(admin.User.Id, start.AddMinutes(index), "still-searching", 1);
        await AddAnswerAsync(admin.User.Id, start.AddHours(1), "chose-recommended-zone", 5, "Me ayudó a elegir.");
        await AddAnswerAsync(admin.User.Id, start.AddHours(2), "chose-another-zone", 4);
        await AddAnswerAsync(admin.User.Id, start.AddHours(3), "visited-recommended-zone", 3);
        await AddAnswerAsync(admin.User.Id, start.AddDays(1).AddSeconds(-1), "stopped-searching", 2);
        await AddAnswerAsync(admin.User.Id, start.AddSeconds(-1), "still-searching", 5);
        await AddAnswerAsync(admin.User.Id, start.AddDays(1), "still-searching", 5);
        await AddAnswerAsync(admin.User.Id, null, null, null);

        const string range = "?from=2040-05-05&to=2040-05-05";
        var first = (await admin.Client.GetFromJsonAsync<AdminSurveyResponse>(Endpoint + range))!;
        first.Items.Should().HaveCount(20);
        first.Items[0].Outcome.Should().Be("stopped-searching");
        first.Items.Should().BeInDescendingOrder(answer => answer.CompletedAt);
        first.Summary.Total.Should().Be(25);
        first.Summary.AverageHelpfulness.Should().Be(1.4);
        first.Summary.Outcomes.Should().Contain(new AdminSurveyOutcomeCount("still-searching", 21));
        first.Items.Should().Contain(answer => answer.Comment == "Me ayudó a elegir.");

        var second = (await admin.Client.GetFromJsonAsync<AdminSurveyResponse>(Endpoint + range + "&page=2"))!;
        second.Items.Should().HaveCount(5);
        second.Summary.Should().BeEquivalentTo(first.Summary);
        second.Items.Select(answer => answer.Id).Should().NotIntersectWith(first.Items.Select(answer => answer.Id));
        second.Items[^1].CompletedAt.Should().Be(start);

        var filtered = (await admin.Client.GetFromJsonAsync<AdminSurveyResponse>(Endpoint + range + "&outcome=chose-recommended-zone&helpfulness=5"))!;
        filtered.Items.Should().ContainSingle().Which.Comment.Should().Be("Me ayudó a elegir.");
        filtered.Summary.Total.Should().Be(1);
        filtered.Summary.AverageHelpfulness.Should().Be(5);

        var empty = (await admin.Client.GetFromJsonAsync<AdminSurveyResponse>(Endpoint + range + "&outcome=chose-recommended-zone&helpfulness=1"))!;
        empty.Items.Should().BeEmpty();
        empty.Summary.Total.Should().Be(0);
        empty.Summary.AverageHelpfulness.Should().BeNull();
        empty.Summary.Outcomes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("from=2040-06-01&to=2040-05-01")]
    [InlineData("to=9999-12-31")]
    [InlineData("outcome=unknown")]
    [InlineData("helpfulness=0")]
    [InlineData("helpfulness=6")]
    [InlineData("page=0")]
    [InlineData("page=1000001")]
    [InlineData("from=not-a-date")]
    public async Task List_RejectsInvalidFilters(string query)
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var admin = await TestApiUser.RegisterAsync(factory);
        admin.AuthenticateAsAdministrator(factory);
        (await admin.Client.GetAsync($"{Endpoint}?{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task AddAnswerAsync(Guid userId, DateTimeOffset? completedAt, string? outcome, int? helpfulness, string? comment = null)
    {
        await using var db = database.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var criteria = new Context { UserId = userId, Type = ContextType.Analysis, PeopleCount = 1, CreatedAt = now, UpdatedAt = now };
        var analysis = new Analysis { UserId = userId, Name = "Encuesta de prueba", Context = criteria, ContextId = criteria.Id, CreatedAt = now, UpdatedAt = now };
        db.Analyses.Add(analysis);
        db.AnalysisFollowUps.Add(new AnalysisFollowUp
        {
            AnalysisId = analysis.Id, UserId = userId, InvitedAt = now,
            CompletedAt = completedAt, Outcome = outcome, Helpfulness = helpfulness, Comment = comment
        });
        await db.SaveChangesAsync();
    }
}
