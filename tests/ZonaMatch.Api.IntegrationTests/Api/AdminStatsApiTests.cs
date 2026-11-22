using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminStatsApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task GetStats_WhenAdministrator_ReturnsStatsIncludingNewUsers()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);

        var response = await administrator.Client.GetAsync("/api/v1/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<AdminStatsResponse>();
        stats!.Users.Total.Should().BeGreaterThanOrEqualTo(2);
        stats.Users.NewLastDay.Should().BeGreaterThanOrEqualTo(2);
        stats.Users.NewLast7Days.Should().BeGreaterThanOrEqualTo(stats.Users.NewLastDay);
        stats.WeeklyRegistrations.Should().HaveCount(GetAdminStats.WeekCount);
        stats.WeeklyRegistrations[^1].Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetStats_WhenAnAccountIsTwoDaysOld_CountsItInTheWeekButNotInTheLastDay()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var before = (await administrator.Client.GetFromJsonAsync<AdminStatsResponse>("/api/v1/admin/stats"))!.Users;

        using var olderAccount = await TestApiUser.RegisterAsync(factory);
        await using (var dbContext = database.CreateDbContext())
        {
            var user = await dbContext.Users.SingleAsync(item => item.Id == olderAccount.User.Id);
            user.CreatedAt = DateTimeOffset.UtcNow.AddDays(-2);
            await dbContext.SaveChangesAsync();
        }

        var after = (await administrator.Client.GetFromJsonAsync<AdminStatsResponse>("/api/v1/admin/stats"))!.Users;

        after.NewLastDay.Should().Be(before.NewLastDay, "an account created two days ago is not from the last 24 hours");
        after.NewLast7Days.Should().Be(before.NewLast7Days + 1);
        after.NewLast30Days.Should().Be(before.NewLast30Days + 1);
    }

    [Fact]
    public async Task GetStats_WhenAUserGivesAPlaceACustomTag_CountsItAsCustom()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var customTag = $"Taller {Guid.NewGuid():N}"[..20];
        var profile = ProfileApiTests.CreateProfileRequest(
            peopleCount: 1, personNames: [], ageRanges: [], criterion: "seguridad", priority: "alta", placeName: "Taller de cerámica");
        profile.Points!.Places[0].Type = customTag;
        (await member.Client.PutAsJsonAsync("/api/v1/profile", profile)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await administrator.Client.GetFromJsonAsync<AdminStatsResponse>("/api/v1/admin/stats");

        var tag = stats!.PlaceTags.Custom.Should().ContainSingle(item => item.Tag == customTag).Subject;
        tag.Users.Should().Be(1);
        tag.SavedAnalyses.Should().Be(0, "the tag is only in the profile, not in a saved analysis");
        stats.PlaceTags.Predefined.Should().NotContain(item => item.Tag == customTag);
    }

    [Fact]
    public async Task GetStats_CountsReportDownloadsOfUsersAndVisitors()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var member = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        using var visitor = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var before = (await administrator.Client.GetFromJsonAsync<AdminStatsResponse>("/api/v1/admin/stats"))!.Activity;

        (await member.Client.PostAsync("/api/v1/report-downloads", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await visitor.PostAsync("/api/v1/report-downloads", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = (await administrator.Client.GetFromJsonAsync<AdminStatsResponse>("/api/v1/admin/stats"))!.Activity;
        after.ReportDownloads.Should().Be(before.ReportDownloads + 2);
        after.ReportDownloadsLast30Days.Should().Be(before.ReportDownloadsLast30Days + 2);
        after.ReportDownloadsByGuests.Should().Be(before.ReportDownloadsByGuests + 1, "only the visitor has no account");
    }

    [Fact]
    public async Task ListGroups_WhenAdministrator_ReturnsTheGroupWithItsCreationDateAndOwner()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        using var owner = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var createResponse = await owner.Client.PostAsJsonAsync("/api/v1/groups", new CreateGroupRequest { Name = "Grupo admin" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<GroupResponse>();

        var response = await administrator.Client.GetAsync("/api/v1/admin/stats/groups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var groups = await response.Content.ReadFromJsonAsync<List<AdminGroupResponse>>();
        var group = groups.Should().ContainSingle(item => item.Id == created!.Id).Subject;
        group.Name.Should().Be("Grupo admin");
        group.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        group.Members.Should().ContainSingle().Which.Should().Be(new AdminGroupMemberResponse(owner.User.Id, owner.User.Email, IsOwner: true));
    }

    [Fact]
    public async Task ListGroups_WhenNotAdministrator_ReturnsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        (await member.Client.GetAsync("/api/v1/admin/stats/groups")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStats_WhenNotAdministrator_ReturnsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        var response = await member.Client.GetAsync("/api/v1/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStats_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/api/v1/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
