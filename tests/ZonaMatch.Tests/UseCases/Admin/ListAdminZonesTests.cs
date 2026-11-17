using FluentAssertions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Admin;

public class ListAdminZonesTests
{
    private static ZoneProfile Zone(string slug, string name, int level = 9, int? crimes = 10, decimal? rent = null, decimal? saleUsd = null) => new()
    {
        Slug = slug,
        Name = name,
        Level = level,
        Population = 1000,
        AreaKm2 = 1,
        CrimeCount = crimes,
        SchoolCount = 2,
        HealthCenterCount = 1,
        AverageRent = rent,
        AverageSalePriceUsd = saleUsd,
    };

    private static ZoneProfile WithCredit(ZoneProfile zone, int listings) => new()
    {
        Slug = zone.Slug,
        Name = zone.Name,
        Level = zone.Level,
        Population = zone.Population,
        AreaKm2 = zone.AreaKm2,
        CrimeCount = zone.CrimeCount,
        CreditSalesBySegment = new Dictionary<(string Type, int Rooms), CreditSales>
        {
            [(HousingTypes.All, 0)] = new CreditSales(listings, [100_000m, 200_000m], [0.5m, 1m]),
        },
    };

    [Fact]
    public async Task ListZones_SortsByNameInSpanishOrder()
    {
        var useCase = new ListAdminZones(new FakeAdminZoneProfilesRepository(
            Zone("villa-urquiza", "Villa Urquiza"), Zone("agronomia", "Agronomía"), Zone("nunez", "Núñez")));

        var result = await useCase.ExecuteAsync();

        result.Zones.Select(zone => zone.Name).Should().Equal("Agronomía", "Núñez", "Villa Urquiza");
    }

    [Fact]
    public async Task ListZones_CountsZonesWithEachKindOfData()
    {
        var useCase = new ListAdminZones(new FakeAdminZoneProfilesRepository(
            Zone("palermo", "Palermo", rent: 900_000, saleUsd: 200_000),
            Zone("comuna-14", "Comuna 14", level: 5, rent: 850_000),
            Zone("san-jose", "San José", crimes: null),
            WithCredit(Zone("caballito", "Caballito"), listings: 40),
            WithCredit(Zone("flores", "Flores"), listings: 0)));

        var coverage = (await useCase.ExecuteAsync()).Coverage;

        coverage.Zones.Should().Be(5);
        coverage.SmallZones.Should().Be(4);
        coverage.LargeZones.Should().Be(1);
        coverage.WithSafety.Should().Be(4);
        coverage.WithRent.Should().Be(2);
        coverage.WithSale.Should().Be(1);
        coverage.WithCreditSales.Should().Be(1, "a zone with no credit-eligible sales published does not count");
    }

    [Fact]
    public async Task ListZones_ReturnsTheCreditListingsOfEveryTypeAndSize()
    {
        var useCase = new ListAdminZones(new FakeAdminZoneProfilesRepository(
            WithCredit(Zone("caballito", "Caballito"), listings: 40), Zone("palermo", "Palermo")));

        var zones = (await useCase.ExecuteAsync()).Zones;

        zones.Single(zone => zone.Slug == "caballito").CreditListings.Should().Be(40);
        zones.Single(zone => zone.Slug == "palermo").CreditListings.Should().BeNull();
    }

    [Fact]
    public async Task ListZones_ScoresEachZoneAgainstZonesOfItsSize()
    {
        // Fewer crimes give a better safety percentile, so the safer zone scores higher.
        var useCase = new ListAdminZones(new FakeAdminZoneProfilesRepository(
            Zone("segura", "Segura", crimes: 5), Zone("insegura", "Insegura", crimes: 50)));

        var zones = (await useCase.ExecuteAsync()).Zones;

        zones.Single(zone => zone.Slug == "segura").Score.Should().BeGreaterThan(zones.Single(zone => zone.Slug == "insegura").Score!.Value);
    }
}
