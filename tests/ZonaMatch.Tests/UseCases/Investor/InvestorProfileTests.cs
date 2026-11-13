using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Investor;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Tests.UseCases.Admin;
using ZonaMatch.Tests.UseCases.Community;
using ZonaMatch.Tests.UseCases.Profile;
using CriterionEntity = ZonaMatch.Domain.Entities.Criterion;

namespace ZonaMatch.Tests.UseCases.Investor;

public class InvestorProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);
    private readonly User _investor = new() { Email = "ana@example.com", InvestorAccess = InvestorAccess.Granted };
    private readonly User _member = new() { Email = "beto@example.com" };
    private readonly FakeAdminUserRepository _users;
    private readonly FakeProfileRepository _profiles = new();
    private readonly Mock<ICriterionRepository> _criteria = new();

    public InvestorProfileTests()
    {
        _users = new FakeAdminUserRepository(_investor, _member);
        _criteria.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CriterionEntity>
        {
            new() { Id = "seguridad", ForHousehold = true, ForInvestor = true },
            new() { Id = "rentabilidad", ForHousehold = false, ForInvestor = true },
            new() { Id = "educacion", ForHousehold = true, ForInvestor = false },
        });
    }

    private SaveMyInvestorProfile Save() => new(_users, _profiles, _criteria.Object, new FixedTimeProvider(Now));

    private static InvestorProfileRequest Request(params (string Id, string Priority)[] criteria) => new()
    {
        Criteria = criteria.ToDictionary(c => c.Id, c => (string?)c.Priority),
        CapitalUsd = 120_000,
        CreditEligible = true,
        PropertyType = HousingTypes.Apartment,
        Rooms = 2,
    };

    [Fact]
    public async Task Save_WhenTheUserHasAccess_StoresAPurchaseProfileOfTypeInvestor()
    {
        var response = await Save().ExecuteAsync(_investor.Id, Request(("rentabilidad", "alta"), ("seguridad", "media")));

        response.Configured.Should().BeTrue();
        response.CapitalUsd.Should().Be(120_000);
        response.Criteria.Should().Equal(new Dictionary<string, string> { ["rentabilidad"] = "alta", ["seguridad"] = "media" });
        var stored = _profiles.Profiles.Should().ContainSingle().Subject;
        stored.Type.Should().Be(ContextType.Investor);
        stored.Operation.Should().Be(HousingOperations.Purchase);
        stored.CreditEligible.Should().BeTrue();
    }

    [Fact]
    public async Task Save_WhenTheUserHasNoAccess_IsForbidden()
    {
        var act = () => Save().ExecuteAsync(_member.Id, Request(("rentabilidad", "alta")));

        await act.Should().ThrowAsync<ForbiddenException>();
        _profiles.Profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_WhenAHouseholdOnlyCriterionIsSent_IsRejected()
    {
        var act = () => Save().ExecuteAsync(_investor.Id, Request(("educacion", "alta")));

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Criterio de inversión desconocido: educacion.");
    }

    [Fact]
    public async Task Get_WhenNotConfiguredYet_ReturnsAnEmptyProfile()
    {
        var response = await new GetMyInvestorProfile(_users, _profiles).ExecuteAsync(_investor.Id);

        response.Configured.Should().BeFalse();
    }

    [Fact]
    public async Task Ranking_RanksOnlySmallZonesWithSalesForTheProfile()
    {
        await Save().ExecuteAsync(_investor.Id, Request(("rentabilidad", "alta")));
        var zones = new Mock<IZoneProfilesRepository>();
        zones.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ZoneProfile>
        {
            new() { Slug = "partido", Name = "Partido", Level = 5, Population = 50_000 },
            Zone("buena", rent: 700_000, price: 90_000),
            Zone("floja", rent: 500_000, price: 150_000),
            new() { Slug = "sin-ventas", Name = "Sin ventas", Level = 8, Population = 1_000 },
        });

        var ranking = await new CalculateInvestmentRanking(_users, _profiles, _criteria.Object, zones.Object, new MarketExchangeRate(1500))
            .ExecuteAsync(_investor.Id);

        ranking.Zones.Select(zone => zone.Slug).Should().Equal("buena", "floja");
        ranking.Zones[0].Metrics.GrossYieldPercent.Should().BeApproximately(6.22, 0.01);
        ranking.UsdArs.Should().Be(1500);
    }

    // Twelve zones with their own sales: zona-12 has the best yield, zona-1 the worst
    private CalculateInvestmentRanking Ranking()
    {
        var zones = new Mock<IZoneProfilesRepository>();
        zones.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 12).Select(i => Zone($"zona-{i}", rent: 400_000 + i * 20_000, price: 100_000)).ToList());
        return new CalculateInvestmentRanking(_users, _profiles, _criteria.Object, zones.Object, new MarketExchangeRate(1500));
    }

    [Fact]
    public async Task Ranking_ShowsTheTopFiveAndRecommendsTheNextOnes()
    {
        await Save().ExecuteAsync(_investor.Id, Request(("rentabilidad", "alta")));

        var ranking = await Ranking().ExecuteAsync(_investor.Id);

        ranking.Zones.Select(zone => zone.Slug).Should().Equal("zona-12", "zona-11", "zona-10", "zona-9", "zona-8");
        ranking.Recommendations.Select(zone => zone.Slug).Should().Equal("zona-7", "zona-6", "zona-5", "zona-4");
        ranking.Criteria.Should().Equal(new Dictionary<string, string> { ["rentabilidad"] = "alta" });
    }

    [Fact]
    public async Task Ranking_AddsTheZonesTheUserChoseInScoreOrder()
    {
        await Save().ExecuteAsync(_investor.Id, Request(("rentabilidad", "alta")));

        var ranking = await Ranking().ExecuteAsync(_investor.Id, new InvestmentRankingRequest { AddedZones = ["zona-1", "zona-7"] });

        ranking.Zones.Select(zone => zone.Slug).Should().Equal("zona-12", "zona-11", "zona-10", "zona-9", "zona-8", "zona-7", "zona-1");
        ranking.Recommendations.Select(zone => zone.Slug).Should().NotContain(["zona-7", "zona-1"]);
    }

    [Fact]
    public async Task Ranking_WithMoreThanTenZones_IsRejected()
    {
        await Save().ExecuteAsync(_investor.Id, Request(("rentabilidad", "alta")));

        var act = () => Ranking().ExecuteAsync(
            _investor.Id, new InvestmentRankingRequest { AddedZones = Enumerable.Range(1, 6).Select(i => $"zona-{i}").ToList() });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Ranking_WithSettingsOfAnAnalysis_UsesThemAndLeavesTheProfileUnchanged()
    {
        await Save().ExecuteAsync(_investor.Id, Request(("rentabilidad", "alta")));
        var settings = Request(("seguridad", "media"));
        settings.CapitalUsd = 90_000;

        var ranking = await Ranking().ExecuteAsync(_investor.Id, new InvestmentRankingRequest { Settings = settings });

        ranking.CapitalUsd.Should().Be(90_000);
        ranking.Criteria.Should().ContainKey("seguridad");
        _profiles.Profiles.Single().Criteria.Single().CriterionId.Should().Be("rentabilidad");
    }

    // Apartment market of 2 rooms, the size of the profile
    private static ZoneProfile Zone(string slug, decimal rent, decimal price)
    {
        var market = new HousingMarket(rent, 30, null, price, 40);
        return new ZoneProfile
        {
            Slug = slug,
            Name = slug,
            Level = 8,
            Population = 10_000,
            MarketByPropertyType = new Dictionary<string, HousingMarket> { [HousingTypes.Apartment] = market },
            MarketByRooms = new Dictionary<(string Type, int Rooms), HousingMarket> { [(HousingTypes.Apartment, 2)] = market },
        };
    }
}
