using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class InvestmentAnalysesApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task InvestmentAnalysis_IsSavedWithItsSettingsAndListedAsInvestor()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var user = await TestApiUser.RegisterAsync(factory);
        await using (var db = database.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == user.User.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.InvestorAccess, InvestorAccess.Granted));
        }

        var request = new SaveInvestmentAnalysisRequest
        {
            Name = "Departamentos para alquilar",
            Settings = new InvestorProfileRequest
            {
                Criteria = new Dictionary<string, string?> { ["rentabilidad"] = "alta" },
                CapitalUsd = 100_000,
                PropertyType = "apartment",
                Rooms = 2,
            },
            Zones = [new AnalysisZoneRequest { Slug = IntegrationZoneRepository.ZoneSlug, Name = "Integration Test Zone" }],
        };

        var created = await user.Client.PostAsJsonAsync("/api/v1/investment-analyses", request);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var analysis = await created.Content.ReadFromJsonAsync<AnalysisResponse>();

        request.Name = null;
        request.Settings.CapitalUsd = 80_000;
        var version = await user.Client.PostAsJsonAsync($"/api/v1/investment-analyses/{analysis!.Id}/versions", request);
        version.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await user.Client.GetFromJsonAsync<List<AnalysisResponse>>("/api/v1/analyses");
        var saved = list.Should().ContainSingle().Subject;
        saved.Profile.Should().Be(AnalysisResponse.InvestorProfile);
        saved.Points.PurchaseLimitUsd.Should().Be(100_000);
        saved.Versions.Should().ContainSingle().Which.Points.PurchaseLimitUsd.Should().Be(80_000);
    }
}
