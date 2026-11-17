using FluentAssertions;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class InvestmentScoringTests
    {
        private const decimal UsdArs = 1500m;

        // Partido with two localidades: the cheap one rents for more than the dear one relative to its price
        private static readonly ZoneProfile Partido = Zone("partido", level: 5, rent: null, price: null, pricePerM2: 2000, listings: null);
        private static readonly ZoneProfile Cheap = Zone("barata", level: 8, rent: 600_000, price: 80_000, pricePerM2: 1500, listings: 200, renting: 40);
        private static readonly ZoneProfile Dear = Zone("cara", level: 8, rent: 700_000, price: 200_000, pricePerM2: 2600, listings: 50, renting: 20);

        private static ZoneProfile Zone(string slug, int level, decimal? rent, decimal? price, decimal? pricePerM2, int? listings, double? renting = null) => new()
        {
            Slug = slug,
            Name = slug,
            Level = level,
            Population = 10_000,
            AverageRent = rent,
            AverageSalePriceUsd = price,
            SalePricePerM2Usd = pricePerM2,
            AvailablePropertiesForSale = listings,
            RentingHouseholdsPercentage = renting,
            ParentSlug = level > 5 ? "partido" : null,
        };

        private static ScoringRequest Request(params string[] criteria) =>
            new(criteria.ToDictionary(criterion => criterion, _ => "alta"), [], null, HousingOperations.Purchase, 500_000);

        [Fact]
        public void MetricsFor_ComputesYieldPaybackPriceAgainstTheParentAndListings()
        {
            var engine = new ScoringEngine([Partido, Cheap, Dear], UsdArs);

            var metrics = engine.MetricsFor(Cheap, null, null);

            // 600.000 * 12 / (80.000 * 1.500)
            metrics.GrossYieldPercent.Should().Be(6.0);
            metrics.PaybackYears.Should().Be(16.7);
            metrics.PriceBelowParentPercent.Should().Be(25.0);
            metrics.ParentName.Should().Be("partido");
            metrics.SaleListingsPerThousandResidents.Should().Be(20.0);
            metrics.RentingHouseholdsPercentage.Should().Be(40);
        }

        [Fact]
        public void MetricsFor_WithoutTheDollarRate_LeavesTheYieldEmpty()
        {
            var engine = new ScoringEngine([Partido, Cheap]);

            engine.MetricsFor(Cheap, null, null).GrossYieldPercent.Should().BeNull();
        }

        [Fact]
        public void Calculate_WhenInvestmentCriteriaArePicked_RanksTheHigherYieldAndCheaperZoneFirst()
        {
            var engine = new ScoringEngine([Partido, Cheap, Dear], UsdArs);
            var request = Request(ScoringEngine.Yield, ScoringEngine.Opportunity, ScoringEngine.Liquidity, ScoringEngine.RentalDemand);

            var cheap = engine.Calculate(Cheap, request);
            var dear = engine.Calculate(Dear, request);

            cheap.Score.Should().BeGreaterThan(dear.Score!.Value);
            cheap.Components.Should().HaveCount(4);
            cheap.Components.Single(c => c.Criterion == ScoringEngine.Yield).Details
                .Should().Be("Rentabilidad bruta 6,0 % anual: el alquiler recupera el precio en 17 años");
            cheap.Components.Single(c => c.Criterion == ScoringEngine.Opportunity).Details
                .Should().Be("El m² cuesta 25 % menos que en partido");
            dear.Components.Single(c => c.Criterion == ScoringEngine.Opportunity).Details
                .Should().Be("El m² cuesta 30 % más que en partido");
        }

        [Fact]
        public void IsKnownCriterion_IncludesTheInvestmentCriteria()
        {
            ScoringEngine.IsKnownCriterion(ScoringEngine.Yield).Should().BeTrue();
            ScoringEngine.IsKnownCriterion(ScoringEngine.RentalDemand).Should().BeTrue();
            ScoringEngine.IsKnownCriterion("inexistente").Should().BeFalse();
        }
    }
}
