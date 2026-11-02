using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class ScoringEngineTests
    {
        private static ZoneProfile CreateZoneProfile(string slug, int crimeCount = 30, int schoolCount = 10, int level = 9, decimal? rent = null, decimal? expenses = null) => new()
        {
            Slug = slug,
            Name = slug,
            Level = level,
            AreaKm2 = 2,
            Population = 1000,
            CrimeCount = crimeCount,
            SchoolCount = schoolCount,
            AverageRent = rent,
            AvailableRentalProperties = rent is null ? null : 10,
            AverageExpenses = expenses,
        };

        private static ScoringRequest CreateScoringRequest(Dictionary<string, string> criteria, decimal? maximumRent = null, List<UserPlace>? places = null) =>
            new(criteria, places ?? [], maximumRent);

        [Fact]
        public void PercentileCountsTiesAsHalf()
        {
            double[] values = [1, 2, 2, 3];

            Assert.Equal(50, ScoringEngine.Percentile(values, 2));
            Assert.Equal(13, ScoringEngine.Percentile(values, 1));
            Assert.Equal(100, ScoringEngine.Percentile(values, 4));
        }

        [Theory]
        [InlineData(0.0, 50.0)]
        [InlineData(0.1, 40.0)]
        [InlineData(0.2, 30.0)]
        [InlineData(0.4, 11.0)]
        [InlineData(1.0, 0.5)]
        public void SmallOveragesDeductOnePointPerPercentAndLargeOveragesDecayExponentially(double overage, double expected)
        {
            Assert.Equal(expected, ScoringEngine.ApplyPenalty(50, overage), 1);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(45, 80)]
        [InlineData(54, 60)]
        [InlineData(63, 22)]
        [InlineData(90, 1)]
        public void TravelScoreIs80AtTheLimitAndPenalizedBeyondIt(int minutes, int expected)
        {
            Assert.Equal(expected, ScoringEngine.TravelScore(minutes, 45));
        }

        [Fact]
        public void TravelWithoutRouteHasNoScore()
        {
            Assert.Null(ScoringEngine.TravelScore(null, 45));
        }

        [Fact]
        public void SafetyRewardsFewerCrimes()
        {
            var safeZone = CreateZoneProfile("segura", crimeCount: 10);
            var unsafeZone = CreateZoneProfile("insegura", crimeCount: 90);
            var engine = new ScoringEngine([safeZone, unsafeZone, CreateZoneProfile("media", crimeCount: 50)]);
            var request = CreateScoringRequest(new() { [ScoringEngine.Safety] = "media" });

            Assert.True(engine.Calculate(safeZone, request).Score > engine.Calculate(unsafeZone, request).Score);
        }

        [Fact]
        public void HighPriorityWeighsNineTimesMoreThanLowPriority()
        {
            // Better on safety (83rd percentile) and worse on education (17th percentile).
            var zone = CreateZoneProfile("zona", crimeCount: 10, schoolCount: 1);
            var engine = new ScoringEngine([zone, CreateZoneProfile("b", crimeCount: 50, schoolCount: 20), CreateZoneProfile("c", crimeCount: 90, schoolCount: 30)]);

            var result = engine.Calculate(zone, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta", [ScoringEngine.Education] = "baja" }));

            Assert.Equal(76, result.Score); // (83 * 9 + 17 * 1) / 10
        }

        [Fact]
        public void MissingCriterionDataIsIgnoredAndNoDataReturnsNullScore()
        {
            var zoneWithoutCrimeData = new ZoneProfile { Slug = "conurbano", Name = "Conurbano", Level = 8, Population = 1000, AreaKm2 = 2, SchoolCount = 5 };
            var engine = new ScoringEngine([zoneWithoutCrimeData, CreateZoneProfile("caba")]);

            var safetyOnlyResult = engine.Calculate(zoneWithoutCrimeData, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta" }));
            var resultWithEducation = engine.Calculate(zoneWithoutCrimeData, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta", [ScoringEngine.Education] = "media" }));

            Assert.Null(safetyOnlyResult.Score);
            Assert.Null(safetyOnlyResult.Components.Single().Score);
            Assert.NotNull(resultWithEducation.Score);
        }

        [Fact]
        public void RentLimitPenalizesSmallOveragesSlightlyAndLargeOveragesHeavily()
        {
            // All zones have the same number of schools, so each scores 50 without a rent limit.
            var slightlyOverBudgetZone = CreateZoneProfile("poco", rent: 1_100_000);
            var atLimitZone = CreateZoneProfile("limite", rent: 1_200_000);
            var wellOverBudgetZone = CreateZoneProfile("mucho", rent: 1_500_000);
            var doubleBudgetZone = CreateZoneProfile("doble", rent: 2_000_000);
            var underBudgetZone = CreateZoneProfile("barata", rent: 500_000);
            var engine = new ScoringEngine([slightlyOverBudgetZone, atLimitZone, wellOverBudgetZone, doubleBudgetZone, underBudgetZone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            int? CalculateWithRentLimit(ZoneProfile zone) => engine.Calculate(zone, CreateScoringRequest(criteria, 1_000_000)).Score;

            Assert.Equal(50, engine.Calculate(slightlyOverBudgetZone, CreateScoringRequest(criteria)).Score);
            Assert.Equal(40, CalculateWithRentLimit(slightlyOverBudgetZone));
            Assert.Equal(30, CalculateWithRentLimit(atLimitZone));
            Assert.Equal(7, CalculateWithRentLimit(wellOverBudgetZone));
            Assert.Equal(1, CalculateWithRentLimit(doubleBudgetZone));
            Assert.Equal(50, CalculateWithRentLimit(underBudgetZone));
            Assert.True(engine.Calculate(underBudgetZone, CreateScoringRequest(criteria, 1_000_000)).Limits.Single().IsSatisfied);
            Assert.Contains("en un 50 %", engine.Calculate(wellOverBudgetZone, CreateScoringRequest(criteria, 1_000_000)).Limits.Single().Details);
        }

        [Fact]
        public void BuildingExpensesCountTowardTheMonthlyLimit()
        {
            // 900,000 rent is within the 1,000,000 limit, but adding 200,000 in expenses exceeds it by 10%.
            var zoneWithExpenses = CreateZoneProfile("con-expensas", rent: 900_000, expenses: 200_000);
            var zoneWithoutExpenses = CreateZoneProfile("sin-expensas", rent: 900_000);
            var engine = new ScoringEngine([zoneWithExpenses, zoneWithoutExpenses]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var limitResult = engine.Calculate(zoneWithExpenses, CreateScoringRequest(criteria, 1_000_000)).Limits.Single();

            Assert.False(limitResult.IsSatisfied);
            Assert.Contains("= $ 1.100.000 por mes", limitResult.Details);
            Assert.Equal(40, engine.Calculate(zoneWithExpenses, CreateScoringRequest(criteria, 1_000_000)).Score);
            Assert.True(engine.Calculate(zoneWithoutExpenses, CreateScoringRequest(criteria, 1_000_000)).Limits.Single().IsSatisfied);
        }

        [Fact]
        public void BuyersComparePurchasePriceAndExpensesAgainstTheirMonthlyLimit()
        {
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageRent = 900_000, AverageExpenses = 200_000, AverageSalePriceUsd = 120_000,
            };
            var engine = new ScoringEngine([zone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var withinBudget = engine.Calculate(zone, new ScoringRequest(criteria, [], 250_000, HousingOperations.Purchase, 150_000));
            var overBudget = engine.Calculate(zone, new ScoringRequest(criteria, [], 250_000, HousingOperations.Purchase, 100_000));

            // Rent does not count; a buyer's monthly housing expense is the building expenses.
            Assert.Equal([ScoringEngine.PurchaseLimit, ScoringEngine.ExpensesLimit], withinBudget.Limits.Select(limit => limit.Type));
            Assert.All(withinBudget.Limits, limit => Assert.True(limit.IsSatisfied));
            Assert.Equal(50, withinBudget.Score);
            Assert.False(overBudget.Limits[0].IsSatisfied);
            Assert.Contains("Precio promedio US$ 120.000: supera tu tope de compra de US$ 100.000 en un 20 %", overBudget.Limits[0].Details);
            Assert.Equal(30, overBudget.Score);
        }

        [Fact]
        public void HousingTypeUsesItsOwnMarketData()
        {
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageRent = 1_500_000, AverageExpenses = 100_000, AverageSalePriceUsd = 200_000,
                MarketByPropertyType = new Dictionary<string, HousingMarket>
                {
                    [HousingTypes.Apartment] = new(700_000, 5, null, 90_000, 8),
                },
            };
            var engine = new ScoringEngine([zone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var purchaseResult = engine.Calculate(zone, new ScoringRequest(criteria, [], null, HousingOperations.Purchase, 95_000, HousingTypes.Apartment));
            var rentResult = engine.Calculate(zone, new ScoringRequest(criteria, [], 750_000, HousingType: HousingTypes.Apartment));
            var houseSaleResult = engine.Calculate(zone, new ScoringRequest(criteria, [], null, HousingOperations.Purchase, 95_000, HousingTypes.House));

            Assert.True(purchaseResult.Limits.Single().IsSatisfied);
            Assert.Contains("Precio promedio de departamentos US$ 90.000", purchaseResult.Limits.Single().Details);
            // Without apartment-specific expenses, use the zone average: 700,000 + 100,000 exceeds 750,000.
            Assert.False(rentResult.Limits.Single().IsSatisfied);
            Assert.Contains("Alquiler de departamentos $ 700.000 + expensas $ 100.000", rentResult.Limits.Single().Details);
            Assert.Null(houseSaleResult.Limits.Single().IsSatisfied);
            Assert.Equal("Sin datos de venta de casas para esta zona", houseSaleResult.Limits.Single().Details);
        }

        [Fact]
        public void HousesAndPhsExcludeBuildingExpenses()
        {
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageExpenses = 100_000,
                MarketByPropertyType = new Dictionary<string, HousingMarket>
                {
                    // The house includes gated-community expenses, which are excluded.
                    [HousingTypes.House] = new(700_000, 5, 570_000, 150_000, 8),
                    [HousingTypes.Ph] = new(600_000, 5, null, 110_000, 8),
                },
            };
            var engine = new ScoringEngine([zone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var houseResult = engine.Calculate(zone, new ScoringRequest(criteria, [], 750_000, HousingType: HousingTypes.House));
            var phResult = engine.Calculate(zone, new ScoringRequest(criteria, [], 1, HousingOperations.Purchase, 120_000, HousingTypes.Ph));

            Assert.True(houseResult.Limits.Single().IsSatisfied);
            Assert.Contains("Alquiler de casas $ 700.000 + expensas $ 0 = $ 700.000 por mes", houseResult.Limits.Single().Details);
            var phExpensesLimit = phResult.Limits.Single(limit => limit.Type == ScoringEngine.ExpensesLimit);
            Assert.True(phExpensesLimit.IsSatisfied);
            Assert.Equal(0, zone.GetMarket(HousingTypes.Ph).AverageExpenses);
        }

        [Fact]
        public void AreaUsesPricePerSquareMeterWhenAvailable()
        {
            // Similar to Ramos Mejia: the average apartment price (US$ 95,354) covers about 66 m²; 34 m² costs much less.
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageExpenses = 100_000,
                MarketByPropertyType = new Dictionary<string, HousingMarket>
                {
                    [HousingTypes.Apartment] = new(624_000, 58, null, 95_354, 59, RentPricePerM2: 13_855, SalePricePerM2Usd: 1_714),
                },
            };
            var engine = new ScoringEngine([zone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var purchaseLimit = engine.Calculate(zone, new ScoringRequest(
                criteria, [], null, HousingOperations.Purchase, 60_000, HousingTypes.Apartment, AreaM2: 34)).Limits.Single();
            var rentLimit = engine.Calculate(zone, new ScoringRequest(
                criteria, [], 600_000, HousingType: HousingTypes.Apartment, AreaM2: 34)).Limits.Single();
            var limitWithoutArea = engine.Calculate(zone, new ScoringRequest(
                criteria, [], null, HousingOperations.Purchase, 60_000, HousingTypes.Apartment)).Limits.Single();

            Assert.True(purchaseLimit.IsSatisfied);
            Assert.Equal("Precio estimado de departamentos para 34 m² US$ 58.276 (US$ 1.714/m²): dentro de tu tope de compra de US$ 60.000", purchaseLimit.Details);
            // 13,855 x 34 = 471,070 rent + 100,000 in building expenses.
            Assert.True(rentLimit.IsSatisfied);
            Assert.Contains("Alquiler estimado de departamentos para 34 m² $ 471.070 + expensas $ 100.000 = $ 571.070 por mes", rentLimit.Details);
            Assert.False(limitWithoutArea.IsSatisfied);
        }

        [Fact]
        public void AreaFallsBackToZoneAverageWithoutPricePerSquareMeter()
        {
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageSalePriceUsd = 90_000,
            };
            var engine = new ScoringEngine([zone]);

            var limitResult = engine.Calculate(zone, new ScoringRequest(
                new Dictionary<string, string> { [ScoringEngine.Education] = "media" }, [], null, HousingOperations.Purchase, 100_000, AreaM2: 34))
                .Limits.Single();

            Assert.Equal("Precio promedio US$ 90.000: dentro de tu tope de compra de US$ 100.000", limitResult.Details);
        }

        [Fact]
        public void MissingRentDataDoesNotAffectTheScore()
        {
            var zone = CreateZoneProfile("zona");
            var engine = new ScoringEngine([zone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var resultWithRentLimit = engine.Calculate(zone, CreateScoringRequest(criteria, 800_000));

            Assert.Equal(engine.Calculate(zone, CreateScoringRequest(criteria)).Score, resultWithRentLimit.Score);
            var limitResult = resultWithRentLimit.Limits.Single();
            Assert.Equal(ScoringEngine.RentLimit, limitResult.Type);
            Assert.Null(limitResult.IsSatisfied);
        }

        [Fact]
        public void LargeZonesAreComparedWithEachOther()
        {
            // The district has more crimes than any neighborhood, but fewer than the other district.
            var district = CreateZoneProfile("partido", crimeCount: 200, level: 5);
            var engine = new ScoringEngine([district, CreateZoneProfile("otro-partido", crimeCount: 300, level: 5), CreateZoneProfile("barrio", crimeCount: 10)]);

            var safetyComponent = engine.Calculate(district, CreateScoringRequest(new() { [ScoringEngine.Safety] = "media" })).Components.Single();

            Assert.Equal(75, safetyComponent.Score);
        }

        [Fact]
        public void PlacesAddAScoringCriterionAndFlagLongTrips()
        {
            var zone = CreateZoneProfile("zona");
            var engine = new ScoringEngine([zone]);
            var places = new List<UserPlace>
            {
                new("Trabajo", -34.6, -58.4, "Muy importante", 45),
                new("Club", -34.6, -58.5, "Baja", 30),
                new("Sin ubicacion", null, null, "Alta", 30),
            };

            var result = engine.Calculate(zone, CreateScoringRequest([], places: places),
                [new TravelTime(30, false), new TravelTime(50, false), null]);

            var component = Assert.Single(result.Components);
            Assert.Equal(ScoringEngine.Places, component.Criterion);
            // Work scores 87 (weight 4); Club is 67% over its limit and scores 6 (weight 1).
            Assert.Equal(71, component.Score);
            var limitResult = Assert.Single(result.Limits);
            Assert.False(limitResult.IsSatisfied);
            Assert.Contains("Club", limitResult.Details);
        }

        [Fact]
        public void PreservesAllPlaceDistancesWithoutChangingScores()
        {
            var zone = CreateZoneProfile("recoleta");
            var engine = new ScoringEngine([zone]);
            var request = CreateScoringRequest([], places:
            [
                new("Trabajo", -34.6, -58.4, "Alta", 45),
                new("Facultad", -34.5, -58.3, "Media", 45),
            ]);

            var resultWithRouteDistances = engine.Calculate(zone, request,
                [new TravelTime(25, false, 8.75), new TravelTime(30, false, 12.5)]);
            var resultWithoutRouteDistances = engine.Calculate(zone, request,
                [new TravelTime(25, false), new TravelTime(30, false)]);

            Assert.Equal(new double?[] { 8.75, 12.5 }, resultWithRouteDistances.Places.Select(place => place.DistanceKm));
            Assert.Equal(resultWithoutRouteDistances.Score, resultWithRouteDistances.Score);
        }

        [Fact]
        public void NoPlacesReturnsAnEmptyList()
        {
            var zone = CreateZoneProfile("recoleta");

            Assert.Empty(new ScoringEngine([zone]).Calculate(zone, CreateScoringRequest([])).Places);
        }

        [Fact]
        public void UnavailableTripDoesNotAffectTheScore()
        {
            var zone = CreateZoneProfile("zona");
            var engine = new ScoringEngine([zone]);
            var places = new List<UserPlace>
            {
                new("Trabajo", -34.6, -58.4, "Alta", 45),
                new("Facultad", -34.6, -58.5, "Alta", 45),
            };

            var result = engine.Calculate(zone, CreateScoringRequest([], places: places), [new TravelTime(45, false), new TravelTime(null, false)]);
            var workOnlyResult = engine.Calculate(zone, CreateScoringRequest([], places: [places[0]]), [new TravelTime(45, false)]);

            Assert.Equal(workOnlyResult.Score, result.Score);
            Assert.Null(result.Places[1].Score);
            Assert.Contains(result.Limits, limit => limit.IsSatisfied is null && limit.Details.Contains("Facultad"));
        }
    }
}
