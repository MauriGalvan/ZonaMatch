using FluentAssertions;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Admin;

public class GetAdminZoneTests
{
    private static ZoneProfile Zone(string slug, int crimes = 10, IReadOnlyDictionary<(string Type, int Rooms), CreditSales>? credit = null) => new()
    {
        Slug = slug,
        Name = slug,
        Level = 9,
        Population = 1000,
        AreaKm2 = 1,
        CrimeCount = crimes,
        SchoolCount = 3,
        HealthCenterCount = 2,
        AverageRent = 900_000,
        AvailableRentalProperties = 120,
        AverageSalePriceUsd = 180_000,
        AvailablePropertiesForSale = 300,
        CreditSalesBySegment = credit ?? new Dictionary<(string Type, int Rooms), CreditSales>(),
    };

    [Fact]
    public async Task GetZone_WhenZoneHasNoData_ThrowsNotFound()
    {
        var useCase = new GetAdminZone(new FakeAdminZoneProfilesRepository(Zone("palermo")));

        var act = () => useCase.ExecuteAsync("atlantida");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetZone_ReturnsTheFiveAnalysisCriteria()
    {
        var useCase = new GetAdminZone(new FakeAdminZoneProfilesRepository(Zone("palermo", crimes: 5), Zone("flores", crimes: 50)));

        var zone = await useCase.ExecuteAsync("palermo");

        zone.Criteria.Select(criterion => criterion.Criterion).Should().Equal(
            ScoringEngine.Safety, ScoringEngine.Mobility, ScoringEngine.GreenSpaces, ScoringEngine.Education, ScoringEngine.Health);
        zone.Criteria[0].Score.Should().Be(75, "the safer of two zones is above the other one and ties with itself");
        zone.Criteria[0].Details.Should().Contain("delitos");
        zone.Score.Should().NotBeNull();
    }

    [Fact]
    public async Task GetZone_ReturnsTheMarket()
    {
        var useCase = new GetAdminZone(new FakeAdminZoneProfilesRepository(Zone("palermo")));

        var market = (await useCase.ExecuteAsync("palermo")).Market;

        market.AverageRent.Should().Be(900_000);
        market.RentListings.Should().Be(120);
        market.AverageSalePriceUsd.Should().Be(180_000);
        market.SaleListings.Should().Be(300);
    }

    [Fact]
    public async Task GetZone_WithoutCreditData_ReturnsNoCredit()
    {
        var useCase = new GetAdminZone(new FakeAdminZoneProfilesRepository(Zone("palermo")));

        (await useCase.ExecuteAsync("palermo")).Credit.Should().BeNull();
    }

    [Fact]
    public async Task GetZone_ReturnsCreditSegmentsInOrderWithTheirPrices()
    {
        var credit = new Dictionary<(string Type, int Rooms), CreditSales>
        {
            [(HousingTypes.Apartment, 2)] = new(12, [80_000m, 120_000m, 160_000m], [0.25m, 0.75m, 1m]),
            [(HousingTypes.All, 0)] = new(40, [70_000m, 250_000m], [0.5m, 1m]),
            [(HousingTypes.House, 0)] = new(0, null, null),
            // Without prices or a count it cannot be used, so it is left out.
            [(HousingTypes.Ph, 3)] = new(null, null, null),
        };
        var useCase = new GetAdminZone(new FakeAdminZoneProfilesRepository(Zone("palermo", credit: credit)));

        var result = (await useCase.ExecuteAsync("palermo")).Credit!;

        result.Listings.Should().Be(40);
        result.Segments.Select(segment => (segment.PropertyType, segment.Rooms)).Should().Equal(
            (HousingTypes.All, 0), (HousingTypes.Apartment, 2), (HousingTypes.House, 0));

        var apartments = result.Segments[1];
        apartments.Listings.Should().Be(12);
        apartments.MinimumPriceUsd.Should().Be(80_000);
        apartments.MedianPriceUsd.Should().Be(100_000, "half of the listings is halfway between 25% and 75%");
        apartments.MaximumPriceUsd.Should().Be(160_000);

        result.Segments[0].MedianPriceUsd.Should().Be(70_000, "half of the listings cost up to the first price");
        result.Segments[2].MedianPriceUsd.Should().BeNull();
    }
}
