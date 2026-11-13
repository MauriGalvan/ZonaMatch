using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Investor;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Tests.UseCases.Admin;
using ZonaMatch.Tests.UseCases.Community;
using ZonaMatch.Tests.UseCases.Profile;
using CriterionEntity = ZonaMatch.Domain.Entities.Criterion;

namespace ZonaMatch.Tests.UseCases.Investor;

public class InvestmentAnalysisTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);
    private readonly User _investor = new() { Email = "ana@example.com", InvestorAccess = InvestorAccess.Granted };
    private readonly User _member = new() { Email = "beto@example.com" };
    private readonly FakeAdminUserRepository _users;
    private readonly Mock<IAnalysisRepository> _analyses = new();
    private readonly Mock<ICriterionRepository> _criteria = new();
    private readonly Mock<IZoneRepository> _zones = new();
    private readonly List<Analysis> _saved = [];

    public InvestmentAnalysisTests()
    {
        _users = new FakeAdminUserRepository(_investor, _member);
        _criteria.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CriterionEntity>
        {
            new() { Id = "rentabilidad", ForHousehold = false, ForInvestor = true },
            new() { Id = "seguridad", ForHousehold = true, ForInvestor = true },
        });
        _zones.Setup(repository => repository.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _analyses.Setup(repository => repository.Add(It.IsAny<Analysis>())).Callback<Analysis>(_saved.Add);
        _analyses.Setup(repository => repository.ListVersionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _saved.Where(analysis => analysis.OriginalAnalysisId is not null).ToList());
        _analyses.Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, CancellationToken _) => _saved.SingleOrDefault(analysis => analysis.Id == id));
    }

    private SaveInvestmentAnalysis Save() =>
        new(_users, _analyses.Object, _criteria.Object, _zones.Object, Ranking(), new FixedTimeProvider(Now));

    private CreateInvestmentAnalysisVersion Version() =>
        new(_users, _analyses.Object, _criteria.Object, _zones.Object, Ranking(), new FixedTimeProvider(Now));

    // Two zones with apartments of 2 rooms for sale and rent; zona-2 rents for more, so it ranks first
    private CalculateInvestmentRanking Ranking(decimal rentOfZone2 = 700_000)
    {
        var profiles = new Mock<IZoneProfilesRepository>();
        profiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ZoneProfile>
        {
            Zone("zona-1", 500_000),
            Zone("zona-2", rentOfZone2),
        });
        return new CalculateInvestmentRanking(_users, new FakeProfileRepository(), _criteria.Object, profiles.Object, new MarketExchangeRate(1500));
    }

    private static ZoneProfile Zone(string slug, decimal rent)
    {
        var market = new HousingMarket(rent, 30, null, 100_000, 40);
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

    private static SaveInvestmentAnalysisRequest Request(string? name = "Departamentos para alquilar", int zones = 2) => new()
    {
        Name = name,
        Settings = new InvestorProfileRequest
        {
            Criteria = new Dictionary<string, string?> { ["rentabilidad"] = "alta" },
            CapitalUsd = 100_000,
            PropertyType = HousingTypes.Apartment,
            Rooms = 2,
        },
        Zones = Enumerable.Range(1, zones).Select(i => new AnalysisZoneRequest { Slug = $"zona-{i}", Name = $"Zona {i}" }).ToList(),
    };

    [Fact]
    public async Task Save_StoresTheZonesWithACopyOfTheSettingsMarkedAsInvestor()
    {
        var response = await Save().ExecuteAsync(_investor.Id, Request());

        response.Profile.Should().Be(AnalysisResponse.InvestorProfile);
        response.Zones.Select(zone => zone.Slug).Should().Equal("zona-1", "zona-2");
        var analysis = _saved.Should().ContainSingle().Subject;
        analysis.Context.Type.Should().Be(ContextType.InvestorAnalysis);
        analysis.Context.MaximumPurchasePriceUsd.Should().Be(100_000);
        analysis.Context.Criteria.Single().CriterionId.Should().Be("rentabilidad");
        // The ranking as it was when saved, shown when the analysis is opened again
        response.InvestmentResult!.Zones.Select(zone => zone.Slug).Should().Equal("zona-2", "zona-1");
        response.Result.Should().BeNull();
    }

    [Fact]
    public async Task Recalculate_SavesTheSameZonesWithTodaysDataAsANewVersion()
    {
        var original = await Save().ExecuteAsync(_investor.Id, Request());
        // The market changed: zona-2 rents for less than zona-1 now
        var recalculate = new RecalculateInvestmentAnalysis(_users, _analyses.Object, Ranking(rentOfZone2: 300_000), new FixedTimeProvider(Now));

        var version = await recalculate.ExecuteAsync(original.Id, _investor.Id);

        version.OriginAnalysisId.Should().Be(original.Id);
        version.Name.Should().StartWith("Departamentos para alquilar · datos al ");
        version.Points.PurchaseLimitUsd.Should().Be(100_000);
        version.InvestmentResult!.Zones.Select(zone => zone.Slug).Should().Equal("zona-1", "zona-2");
        _saved.Single(analysis => analysis.Id == original.Id).ResultJson.Should().Contain("zona-2");
    }

    [Fact]
    public async Task Save_WithoutInvestorAccess_IsForbidden()
    {
        var act = () => Save().ExecuteAsync(_member.Id, Request());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Save_WithMoreThanTenZones_IsRejected()
    {
        var act = () => Save().ExecuteAsync(_investor.Id, Request(zones: 11));

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Version_HangsFromTheOriginalWithItsOwnSettings()
    {
        var original = await Save().ExecuteAsync(_investor.Id, Request());
        var adjusted = Request(name: null);
        adjusted.Settings.CapitalUsd = 80_000;

        var version = await Version().ExecuteAsync(original.Id, _investor.Id, adjusted);

        version.OriginAnalysisId.Should().Be(original.Id);
        version.Name.Should().Be("Departamentos para alquilar (2)");
        _saved.Single(analysis => analysis.Id == original.Id).Context.MaximumPurchasePriceUsd.Should().Be(100_000);
        _saved.Single(analysis => analysis.Id == version.Id).Context.MaximumPurchasePriceUsd.Should().Be(80_000);
    }

    [Fact]
    public async Task HouseholdRecalculation_OfAnInvestmentAnalysis_IsRejected()
    {
        var original = await Save().ExecuteAsync(_investor.Id, Request());
        var recalculate = new RecalculateAnalysis(_analyses.Object, Mock.Of<IAnalysisResultCalculator>(), new FixedTimeProvider(Now));

        var act = () => recalculate.ExecuteAsync(original.Id, _investor.Id);

        await act.Should().ThrowAsync<BadRequestException>();
    }
}
