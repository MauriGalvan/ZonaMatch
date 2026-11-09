using FluentAssertions;
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
        public void Percentile_WhenValuesContainTies_CountsEachTieAsHalf()
        {
            double[] values = [1, 2, 2, 3];

            ScoringEngine.Percentile(values, 2).Should().Be(50);
            ScoringEngine.Percentile(values, 1).Should().Be(13);
            ScoringEngine.Percentile(values, 4).Should().Be(100);
        }

        [Theory]
        [InlineData(0.0, 50.0)]
        [InlineData(0.1, 40.0)]
        [InlineData(0.2, 30.0)]
        [InlineData(0.4, 11.0)]
        [InlineData(1.0, 0.5)]
        public void ApplyPenalty_WhenOverageChanges_ReturnsExpectedPenalty(double overage, double expected)
        {
            Math.Round(ScoringEngine.ApplyPenalty(50, overage), 1).Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(45, 80)]
        [InlineData(54, 60)]
        [InlineData(63, 22)]
        [InlineData(90, 1)]
        public void TravelScore_WhenTravelTimeChanges_AppliesLimitAndPenalty(int minutes, int expected)
        {
            ScoringEngine.TravelScore(minutes, 45).Should().Be(expected);
        }

        [Fact]
        public void TravelScore_WhenRouteIsUnavailable_ReturnsNoScore()
        {
            ScoringEngine.TravelScore(null, 45).Should().BeNull();
        }

        [Fact]
        public void Calculate_WhenZoneHasFewerCrimes_ScoresItHigher()
        {
            var safeZone = CreateZoneProfile("segura", crimeCount: 10);
            var unsafeZone = CreateZoneProfile("insegura", crimeCount: 90);
            var engine = new ScoringEngine([safeZone, unsafeZone, CreateZoneProfile("media", crimeCount: 50)]);
            var request = CreateScoringRequest(new() { [ScoringEngine.Safety] = "media" });

            var safeScore = engine.Calculate(safeZone, request).Score;
            var unsafeScore = engine.Calculate(unsafeZone, request).Score;

            safeScore.Should().NotBeNull();
            unsafeScore.Should().NotBeNull();
            safeScore!.Value.Should().BeGreaterThan(unsafeScore!.Value);
        }

        [Fact]
        public void Calculate_WhenPrioritiesDiffer_WeightsHighPriorityNineTimesMore()
        {
            // Better on safety (83rd percentile) and worse on education (17th percentile).
            var zone = CreateZoneProfile("zona", crimeCount: 10, schoolCount: 1);
            var engine = new ScoringEngine([zone, CreateZoneProfile("b", crimeCount: 50, schoolCount: 20), CreateZoneProfile("c", crimeCount: 90, schoolCount: 30)]);

            var result = engine.Calculate(zone, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta", [ScoringEngine.Education] = "baja" }));

            result.Score.Should().Be(76); // (83 * 9 + 17 * 1) / 10
        }

        [Fact]
        public void Calculate_WhenCriterionDataIsMissing_IgnoresItAndReturnsNullWhenNoDataRemains()
        {
            var zoneWithoutCrimeData = new ZoneProfile { Slug = "conurbano", Name = "Conurbano", Level = 8, Population = 1000, AreaKm2 = 2, SchoolCount = 5 };
            var engine = new ScoringEngine([zoneWithoutCrimeData, CreateZoneProfile("caba")]);

            var safetyOnlyResult = engine.Calculate(zoneWithoutCrimeData, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta" }));
            var resultWithEducation = engine.Calculate(zoneWithoutCrimeData, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta", [ScoringEngine.Education] = "media" }));

            safetyOnlyResult.Score.Should().BeNull();
            safetyOnlyResult.Components.Single().Score.Should().BeNull();
            resultWithEducation.Score.Should().NotBeNull();
        }

        [Fact]
        public void Calculate_WhenRentExceedsMonthlyLimit_PenalizesSmallAndLargeOverages()
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

            engine.Calculate(slightlyOverBudgetZone, CreateScoringRequest(criteria)).Score.Should().Be(50);
            CalculateWithRentLimit(slightlyOverBudgetZone).Should().Be(40);
            CalculateWithRentLimit(atLimitZone).Should().Be(30);
            CalculateWithRentLimit(wellOverBudgetZone).Should().Be(7);
            CalculateWithRentLimit(doubleBudgetZone).Should().Be(1);
            CalculateWithRentLimit(underBudgetZone).Should().Be(50);
            engine.Calculate(underBudgetZone, CreateScoringRequest(criteria, 1_000_000)).Limits.Single().IsSatisfied.Should().BeTrue();
            engine.Calculate(wellOverBudgetZone, CreateScoringRequest(criteria, 1_000_000)).Limits.Single().Details.Should().Contain("en un 50 %");
        }

        [Fact]
        public void Calculate_WhenRentAndExpensesExceedMonthlyLimit_IncludesExpenses()
        {
            // 900,000 rent is within the 1,000,000 limit, but adding 200,000 in expenses exceeds it by 10%.
            var zoneWithExpenses = CreateZoneProfile("con-expensas", rent: 900_000, expenses: 200_000);
            var zoneWithoutExpenses = CreateZoneProfile("sin-expensas", rent: 900_000);
            var engine = new ScoringEngine([zoneWithExpenses, zoneWithoutExpenses]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var limitResult = engine.Calculate(zoneWithExpenses, CreateScoringRequest(criteria, 1_000_000)).Limits.Single();

            limitResult.IsSatisfied.Should().BeFalse();
            limitResult.Details.Should().Contain("= $ 1.100.000 por mes");
            engine.Calculate(zoneWithExpenses, CreateScoringRequest(criteria, 1_000_000)).Score.Should().Be(40);
            engine.Calculate(zoneWithoutExpenses, CreateScoringRequest(criteria, 1_000_000)).Limits.Single().IsSatisfied.Should().BeTrue();
        }

        [Fact]
        public void Calculate_WhenBuyerHasPurchaseAndMonthlyLimits_AppliesEachLimit()
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
            withinBudget.Limits.Select(limit => limit.Type).Should().Equal(ScoringEngine.PurchaseLimit, ScoringEngine.ExpensesLimit);
            withinBudget.Limits.Should().AllSatisfy(limit => limit.IsSatisfied.Should().BeTrue());
            withinBudget.Score.Should().Be(50);
            overBudget.Limits[0].IsSatisfied.Should().BeFalse();
            overBudget.Limits[0].Details.Should().Contain("Precio promedio US$ 120.000: supera tu tope de compra de US$ 100.000 en un 20 %");
            overBudget.Score.Should().Be(30);
        }

        [Fact]
        public void Calculate_WhenHousingTypeHasMarketData_UsesItsOwnMarketData()
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

            purchaseResult.Limits.Single().IsSatisfied.Should().BeTrue();
            purchaseResult.Limits.Single().Details.Should().Contain("Precio promedio de departamentos US$ 90.000");
            // Without apartment-specific expenses, use the zone average: 700,000 + 100,000 exceeds 750,000.
            rentResult.Limits.Single().IsSatisfied.Should().BeFalse();
            rentResult.Limits.Single().Details.Should().Contain("Alquiler de departamentos $ 700.000 + expensas $ 100.000");
            houseSaleResult.Limits.Single().IsSatisfied.Should().BeNull();
            houseSaleResult.Limits.Single().Details.Should().Be("Sin datos de venta de casas para esta zona");
        }

        [Fact]
        public void Calculate_WhenHousingTypeIsHouseOrPh_ExcludesBuildingExpenses()
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

            houseResult.Limits.Single().IsSatisfied.Should().BeTrue();
            houseResult.Limits.Single().Details.Should().Contain("Alquiler de casas $ 700.000 + expensas $ 0 = $ 700.000 por mes");
            var phExpensesLimit = phResult.Limits.Single(limit => limit.Type == ScoringEngine.ExpensesLimit);
            phExpensesLimit.IsSatisfied.Should().BeTrue();
            zone.GetMarket(HousingTypes.Ph).AverageExpenses.Should().Be(0);
        }

        [Fact]
        public void Calculate_WhenPricePerSquareMeterIsAvailable_UsesItForRequestedArea()
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

            purchaseLimit.IsSatisfied.Should().BeTrue();
            purchaseLimit.Details.Should().Be("Precio estimado de departamentos para 34 m² US$ 58.276 (US$ 1.714/m²): dentro de tu tope de compra de US$ 60.000");
            // 13,855 x 34 = 471,070 rent + 100,000 in building expenses.
            rentLimit.IsSatisfied.Should().BeTrue();
            rentLimit.Details.Should().Contain("Alquiler estimado de departamentos para 34 m² $ 471.070 + expensas $ 100.000 = $ 571.070 por mes");
            limitWithoutArea.IsSatisfied.Should().BeFalse();
        }

        [Fact]
        public void Calculate_WhenPricePerSquareMeterIsMissing_UsesZoneAverage()
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

            limitResult.Details.Should().Be("Precio promedio US$ 90.000: dentro de tu tope de compra de US$ 100.000");
        }

        [Fact]
        public void Calculate_WhenRentDataIsMissing_DoesNotAffectScore()
        {
            var zone = CreateZoneProfile("zona");
            var engine = new ScoringEngine([zone]);
            var criteria = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var resultWithRentLimit = engine.Calculate(zone, CreateScoringRequest(criteria, 800_000));

            resultWithRentLimit.Score.Should().Be(engine.Calculate(zone, CreateScoringRequest(criteria)).Score);
            var limitResult = resultWithRentLimit.Limits.Single();
            limitResult.Type.Should().Be(ScoringEngine.RentLimit);
            limitResult.IsSatisfied.Should().BeNull();
        }

        [Fact]
        public void Calculate_WhenZoneIsLarge_ComparesItWithOtherLargeZones()
        {
            // The district has more crimes than any neighborhood, but fewer than the other district.
            var district = CreateZoneProfile("partido", crimeCount: 200, level: 5);
            var engine = new ScoringEngine([district, CreateZoneProfile("otro-partido", crimeCount: 300, level: 5), CreateZoneProfile("barrio", crimeCount: 10)]);

            var safetyComponent = engine.Calculate(district, CreateScoringRequest(new() { [ScoringEngine.Safety] = "media" })).Components.Single();

            safetyComponent.Score.Should().Be(75);
        }

        [Fact]
        public void Calculate_WhenUserPlacesAreProvided_AddsCriterionAndFlagsLongTrips()
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

            var component = result.Components.Should().ContainSingle().Which;
            component.Criterion.Should().Be(ScoringEngine.Places);
            // Work scores 87 (weight 4); Club is 67% over its limit and scores 6 (weight 1).
            component.Score.Should().Be(71);
            var limitResult = result.Limits.Should().ContainSingle().Which;
            limitResult.IsSatisfied.Should().BeFalse();
            limitResult.Details.Should().Contain("Club");
        }

        [Fact]
        public void Calculate_WhenRouteDistancesAreAvailable_PreservesDistancesWithoutChangingScores()
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

            resultWithRouteDistances.Places.Select(place => place.DistanceKm).Should().Equal(8.75, 12.5);
            resultWithRouteDistances.Score.Should().Be(resultWithoutRouteDistances.Score);
        }

        [Fact]
        public void Calculate_WhenNoPlacesAreProvided_ReturnsNoPlaceScores()
        {
            var zone = CreateZoneProfile("recoleta");

            new ScoringEngine([zone]).Calculate(zone, CreateScoringRequest([])).Places.Should().BeEmpty();
        }

        [Fact]
        public void Calculate_WhenTripIsUnavailable_DoesNotAffectScore()
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

            result.Score.Should().Be(workOnlyResult.Score);
            result.Places[1].Score.Should().BeNull();
            result.Limits.Should().Contain(limit => limit.IsSatisfied == null && limit.Details.Contains("Facultad"));
        }

        private static readonly Dictionary<string, string> EducationCriteria = new() { [ScoringEngine.Education] = "media" };

        // Sale prices from US$ 100.000 to US$ 200.000, every tenth of the listings US$ 10.000 more (older data, without shares)
        private static ZoneProfile ZoneWithSaleRange() => new()
        {
            Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
            AverageSalePriceUsd = 160_000,
            SaleDecilesUsd = [100_000, 110_000, 120_000, 130_000, 140_000, 150_000, 160_000, 170_000, 180_000, 190_000, 200_000],
        };

        // Like Palermo: apartments for rent average $ 1.289.000, but a 2-room one is about $ 1.229.000
        private static ZoneProfile ZoneWithRooms() => new()
        {
            Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
            AverageExpenses = 280_000,
            MarketByPropertyType = new Dictionary<string, HousingMarket>
            {
                [HousingTypes.Apartment] = new(1_289_000, 3_413, 291_000, 258_200, 11_967, RentPricePerM2: 25_259, SalePricePerM2Usd: 3_910),
            },
            MarketByRooms = new Dictionary<(string Type, int Rooms), HousingMarket>
            {
                [(HousingTypes.Apartment, 2)] = new(1_229_000, 1_353, 250_000, 170_547, 3_560, RentPricePerM2: 25_523, SalePricePerM2Usd: 3_738),
            },
        };

        [Fact]
        public void Calculate_WhenPartOfTheListingsFitThePurchaseCap_MeetsTheLimitAndDescribesTheRange()
        {
            var zone = ZoneWithSaleRange();
            var engine = new ScoringEngine([zone]);

            // The average (160.000) is over the limit, but half of the listings fit: the limit is met and 10 points go
            var result = engine.Calculate(zone, new ScoringRequest(EducationCriteria, [], null, HousingOperations.Purchase, 150_000));

            var limit = result.Limits.Single();
            limit.IsSatisfied.Should().BeTrue();
            result.Score.Should().Be(40);
            limit.Details.Should().Be(
                "Precio: de US$ 100.000 a US$ 200.000, precio medio US$ 150.000: entra el 50 % de las publicaciones en tu tope de compra de US$ 150.000");
        }

        [Fact]
        public void Calculate_WhenNoListingFitsThePurchaseCap_FailsTheLimit()
        {
            var zone = ZoneWithSaleRange();

            var result = new ScoringEngine([zone]).Calculate(zone, new ScoringRequest(EducationCriteria, [], null, HousingOperations.Purchase, 90_000));

            result.Limits.Single().IsSatisfied.Should().BeFalse();
        }

        [Fact]
        public void Calculate_WhenSharesAreKnown_UsesTheRealShareOfTheCap()
        {
            // Many rentals at exactly $ 500.000: 39 % cost up to it although the point is at 35 %
            var zone = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageRent = 520_000,
                RentDeciles = [250_000, 400_000, 490_000, 500_000, 520_000, 600_000, 1_000_000],
                RentShares = [0.01m, 0.2m, 0.33m, 0.39m, 0.45m, 0.7m, 1],
            };

            var result = new ScoringEngine([zone]).Calculate(zone, new ScoringRequest(EducationCriteria, [], 500_000));

            result.Limits.Single().Details.Should().EndWith("entra el 39 % de las publicaciones en tu tope de $ 500.000");
        }

        [Fact]
        public void Calculate_WhenRoomsAreRequested_ComparesTheBudgetWithThatSize()
        {
            var zone = ZoneWithRooms();

            var result = new ScoringEngine([zone]).Calculate(zone, new ScoringRequest(
                EducationCriteria, [], 1_500_000, HousingType: HousingTypes.Apartment, Rooms: 2));

            // 1.229.000 of rent + 250.000 of expenses
            var limit = result.Limits.Single();
            limit.IsSatisfied.Should().BeTrue();
            limit.Details.Should().Be(
                "Alquiler de departamentos de 2 ambientes $ 1.229.000 + expensas $ 250.000 = $ 1.479.000 por mes: dentro de tu tope de $ 1.500.000");
        }

        [Fact]
        public void Calculate_WhenRoomsAndSurfaceAreRequested_EstimatesWithThePricePerM2OfThatSize()
        {
            var zone = ZoneWithRooms();

            var result = new ScoringEngine([zone]).Calculate(zone, new ScoringRequest(
                EducationCriteria, [], null, HousingOperations.Purchase, 150_000, HousingTypes.Apartment, AreaM2: 40, Rooms: 2));

            // 3.738 x 40 = 149.520
            var limit = result.Limits.Single();
            limit.IsSatisfied.Should().BeTrue();
            limit.Details.Should().StartWith("Precio estimado de departamentos de 2 ambientes para 40 m² US$ 149.520 (US$ 3.738/m²)");
        }

        // Like Ramos Mejía: sales from US$ 18.000; credit-eligible ones read at their own price points. Every type and
        // size, apartments of every size, and 2-room apartments, which are cheaper; 1-room houses have none published.
        private static ZoneProfile ZoneWithCreditSales(bool withCredit = true) => new()
        {
            Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
            AverageSalePriceUsd = 160_000,
            SaleDecilesUsd = [18_000, 60_000, 95_000, 145_000, 400_000],
            CreditSalesBySegment = withCredit
                ? new Dictionary<(string Type, int Rooms), CreditSales>
                {
                    [(HousingTypes.All, 0)] = new(2348, [46_000, 55_000, 73_000, 115_000, 180_000], [0.047m, 0.130m, 0.280m, 0.545m, 0.787m]),
                    [(HousingTypes.Apartment, 0)] = new(1303, [44_000, 64_000, 89_000, 140_000], [0.051m, 0.272m, 0.550m, 0.797m]),
                    [(HousingTypes.Apartment, 2)] = new(578, [43_000, 55_000, 72_000, 95_000], [0.052m, 0.299m, 0.588m, 0.817m]),
                    [(HousingTypes.House, 1)] = new(0, null, null),
                }
                : new Dictionary<(string Type, int Rooms), CreditSales>(),
        };

        private static ScoringRequest CreditPurchase(decimal cap, string? housingType = null, int? rooms = null) =>
            new(EducationCriteria, [], null, HousingOperations.Purchase, cap, housingType, Rooms: rooms, CreditEligible: true);

        [Fact]
        public void Calculate_WhenBuyingWithAMortgage_UsesTheCreditSalesOfTheTypeAndSize()
        {
            var zone = ZoneWithCreditSales();

            var limit = new ScoringEngine([zone]).Calculate(zone, CreditPurchase(95_000, HousingTypes.Apartment, 2)).Limits.Single();

            limit.IsSatisfied.Should().BeTrue();
            limit.Details.Should().Be("Ventas apto crédito de departamentos de 2 ambientes: entra el 82 % de las publicaciones apto crédito en tu tope de compra de US$ 95.000");
        }

        [Fact]
        public void Calculate_WhenTheSizeHasNoCreditData_UsesTheTypeInEverySizeAndSaysSo()
        {
            var zone = ZoneWithCreditSales();

            var limit = new ScoringEngine([zone]).Calculate(zone, CreditPurchase(140_000, HousingTypes.Apartment, 3)).Limits.Single();

            limit.Details.Should().Be("Ventas apto crédito de departamentos de todos los tamaños (sin datos suficientes de 3 ambientes): " +
                                      "entra el 80 % de las publicaciones apto crédito en tu tope de compra de US$ 140.000");
        }

        [Fact]
        public void Calculate_WhenTheTypeHasNoCreditData_UsesEveryTypeAndSaysSo()
        {
            var zone = ZoneWithCreditSales();

            var limit = new ScoringEngine([zone]).Calculate(zone, CreditPurchase(115_000, HousingTypes.Ph)).Limits.Single();

            limit.Details.Should().StartWith("Ventas apto crédito de todos los tipos (sin datos suficientes de PH): entra el 55 %");
        }

        [Fact]
        public void Calculate_WhenTheSegmentHasNoCreditSalesPublished_FailsTheLimitBelowNoneFitting()
        {
            var zone = ZoneWithCreditSales();
            var engine = new ScoringEngine([zone]);

            var none = engine.Calculate(zone, CreditPurchase(200_000, HousingTypes.House, 1));
            // 2 % of the credit-eligible sales fit: the limit is met with almost the whole deduction
            var fewFit = engine.Calculate(zone, CreditPurchase(23_000));

            var limit = none.Limits.Single();
            limit.IsSatisfied.Should().BeFalse();
            limit.Details.Should().Be("Ventas apto crédito de casas de 1 ambiente: no hay publicadas en esta zona");
            none.Score.Should().BeLessThan(fewFit.Score!.Value);
        }

        [Fact]
        public void Calculate_WhenBuyingWithAMortgage_ComparesTheCapWithTheCreditEligibleSales()
        {
            var zone = ZoneWithCreditSales();

            var limit = new ScoringEngine([zone]).Calculate(zone, CreditPurchase(115_000)).Limits.Single();

            limit.IsSatisfied.Should().BeTrue();
            limit.Details.Should().Be("Ventas apto crédito: entra el 55 % de las publicaciones apto crédito en tu tope de compra de US$ 115.000");
        }

        [Fact]
        public void Calculate_WhenTheCapIsBelowTheFirstCreditPrice_InterpolatesFromZero()
        {
            var zone = ZoneWithCreditSales();

            // Half of US$ 46.000: half of the 4,7 % up to it
            var limit = new ScoringEngine([zone]).Calculate(zone, CreditPurchase(23_000)).Limits.Single();

            limit.Details.Should().EndWith("entra el 2 % de las publicaciones apto crédito en tu tope de compra de US$ 23.000");
        }

        [Fact]
        public void Calculate_WhenNoCreditEligibleSaleFits_FailsTheLimit()
        {
            var zone = ZoneWithCreditSales();

            var limit = new ScoringEngine([zone]).Calculate(zone, CreditPurchase(1_000)).Limits.Single();

            limit.IsSatisfied.Should().BeFalse();
            limit.Details.Should().StartWith("Ventas apto crédito: ninguna entra");
        }

        [Fact]
        public void Calculate_WhenZoneHasNoCreditData_UsesEverySaleAndSaysSo()
        {
            var zone = ZoneWithCreditSales(withCredit: false);

            var limit = new ScoringEngine([zone]).Calculate(zone, CreditPurchase(115_000)).Limits.Single();

            limit.Details.Should().StartWith("Precio: de US$ 18.000")
                .And.EndWith("(sin datos de ventas apto crédito en esta zona)");
        }
    }
}
