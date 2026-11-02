using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class ScoringEngineTests
    {
        private static ZoneProfile CreateZoneProfile(string slug, int delitos = 30, int escuelas = 10, int nivel = 9, decimal? alquiler = null, decimal? expensas = null) => new()
        {
            Slug = slug,
            Name = slug,
            Level = nivel,
            AreaKm2 = 2,
            Population = 1000,
            CrimeCount = delitos,
            SchoolCount = escuelas,
            AverageRent = alquiler,
            AvailableRentalProperties = alquiler is null ? null : 10,
            AverageExpenses = expensas,
        };

        private static ScoringRequest CreateScoringRequest(Dictionary<string, string> criterios, decimal? alquilerMaximo = null, List<UserPlace>? lugares = null) =>
            new(criterios, lugares ?? [], alquilerMaximo);

        [Fact]
        public void PercentileCountsTiesAsHalf()
        {
            double[] valores = [1, 2, 2, 3];

            Assert.Equal(50, ScoringEngine.Percentile(valores, 2));
            Assert.Equal(13, ScoringEngine.Percentile(valores, 1));
            Assert.Equal(100, ScoringEngine.Percentile(valores, 4));
        }

        [Theory]
        [InlineData(0.0, 50.0)]
        [InlineData(0.1, 40.0)]
        [InlineData(0.2, 30.0)]
        [InlineData(0.4, 11.0)]
        [InlineData(1.0, 0.5)]
        public void SmallOveragesDeductOnePointPerPercentAndLargeOveragesDecayExponentially(double exceso, double esperado)
        {
            Assert.Equal(esperado, ScoringEngine.ApplyPenalty(50, exceso), 1);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(45, 80)]
        [InlineData(54, 60)]
        [InlineData(63, 22)]
        [InlineData(90, 1)]
        public void TravelScoreIs80AtTheLimitAndPenalizedBeyondIt(int minutos, int esperado)
        {
            Assert.Equal(esperado, ScoringEngine.TravelScore(minutos, 45));
        }

        [Fact]
        public void TravelWithoutRouteHasNoScore()
        {
            Assert.Null(ScoringEngine.TravelScore(null, 45));
        }

        [Fact]
        public void SafetyRewardsFewerCrimes()
        {
            var segura = CreateZoneProfile("segura", delitos: 10);
            var insegura = CreateZoneProfile("insegura", delitos: 90);
            var motor = new ScoringEngine([segura, insegura, CreateZoneProfile("media", delitos: 50)]);
            var pedido = CreateScoringRequest(new() { [ScoringEngine.Safety] = "media" });

            Assert.True(motor.Calculate(segura, pedido).Score > motor.Calculate(insegura, pedido).Score);
        }

        [Fact]
        public void HighPriorityWeighsNineTimesMoreThanLowPriority()
        {
            // Better on safety (83rd percentile) and worse on education (17th percentile).
            var zona = CreateZoneProfile("zona", delitos: 10, escuelas: 1);
            var motor = new ScoringEngine([zona, CreateZoneProfile("b", delitos: 50, escuelas: 20), CreateZoneProfile("c", delitos: 90, escuelas: 30)]);

            var resultado = motor.Calculate(zona, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta", [ScoringEngine.Education] = "baja" }));

            Assert.Equal(76, resultado.Score); // (83 * 9 + 17 * 1) / 10
        }

        [Fact]
        public void MissingCriterionDataIsIgnoredAndNoDataReturnsNullScore()
        {
            var sinDelitos = new ZoneProfile { Slug = "conurbano", Name = "Conurbano", Level = 8, Population = 1000, AreaKm2 = 2, SchoolCount = 5 };
            var motor = new ScoringEngine([sinDelitos, CreateZoneProfile("caba")]);

            var soloSeguridad = motor.Calculate(sinDelitos, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta" }));
            var conEducacion = motor.Calculate(sinDelitos, CreateScoringRequest(new() { [ScoringEngine.Safety] = "alta", [ScoringEngine.Education] = "media" }));

            Assert.Null(soloSeguridad.Score);
            Assert.Null(soloSeguridad.Components.Single().Score);
            Assert.NotNull(conEducacion.Score);
        }

        [Fact]
        public void RentLimitPenalizesSmallOveragesSlightlyAndLargeOveragesHeavily()
        {
            // All zones have the same number of schools, so each scores 50 without a rent limit.
            var poco = CreateZoneProfile("poco", alquiler: 1_100_000);
            var limite = CreateZoneProfile("limite", alquiler: 1_200_000);
            var mucho = CreateZoneProfile("mucho", alquiler: 1_500_000);
            var doble = CreateZoneProfile("doble", alquiler: 2_000_000);
            var barata = CreateZoneProfile("barata", alquiler: 500_000);
            var motor = new ScoringEngine([poco, limite, mucho, doble, barata]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            int? ConTope(ZoneProfile z) => motor.Calculate(z, CreateScoringRequest(criterios, 1_000_000)).Score;

            Assert.Equal(50, motor.Calculate(poco, CreateScoringRequest(criterios)).Score);
            Assert.Equal(40, ConTope(poco));
            Assert.Equal(30, ConTope(limite));
            Assert.Equal(7, ConTope(mucho));
            Assert.Equal(1, ConTope(doble));
            Assert.Equal(50, ConTope(barata));
            Assert.True(motor.Calculate(barata, CreateScoringRequest(criterios, 1_000_000)).Limits.Single().IsSatisfied);
            Assert.Contains("en un 50 %", motor.Calculate(mucho, CreateScoringRequest(criterios, 1_000_000)).Limits.Single().Details);
        }

        [Fact]
        public void BuildingExpensesCountTowardTheMonthlyLimit()
        {
            // 900,000 rent is within the 1,000,000 limit, but adding 200,000 in expenses exceeds it by 10%.
            var conExpensas = CreateZoneProfile("con-expensas", alquiler: 900_000, expensas: 200_000);
            var sinExpensas = CreateZoneProfile("sin-expensas", alquiler: 900_000);
            var motor = new ScoringEngine([conExpensas, sinExpensas]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var limite = motor.Calculate(conExpensas, CreateScoringRequest(criterios, 1_000_000)).Limits.Single();

            Assert.False(limite.IsSatisfied);
            Assert.Contains("= $ 1.100.000 por mes", limite.Details);
            Assert.Equal(40, motor.Calculate(conExpensas, CreateScoringRequest(criterios, 1_000_000)).Score);
            Assert.True(motor.Calculate(sinExpensas, CreateScoringRequest(criterios, 1_000_000)).Limits.Single().IsSatisfied);
        }

        [Fact]
        public void BuyersComparePurchasePriceAndExpensesAgainstTheirMonthlyLimit()
        {
            var zona = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageRent = 900_000, AverageExpenses = 200_000, AverageSalePriceUsd = 120_000,
            };
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var dentro = motor.Calculate(zona, new ScoringRequest(criterios, [], 250_000, HousingOperations.Purchase, 150_000));
            var fuera = motor.Calculate(zona, new ScoringRequest(criterios, [], 250_000, HousingOperations.Purchase, 100_000));

            // Rent does not count; a buyer's monthly housing expense is the building expenses.
            Assert.Equal([ScoringEngine.PurchaseLimit, ScoringEngine.ExpensesLimit], dentro.Limits.Select(l => l.Type));
            Assert.All(dentro.Limits, l => Assert.True(l.IsSatisfied));
            Assert.Equal(50, dentro.Score);
            Assert.False(fuera.Limits[0].IsSatisfied);
            Assert.Contains("Precio promedio US$ 120.000: supera tu tope de compra de US$ 100.000 en un 20 %", fuera.Limits[0].Details);
            Assert.Equal(30, fuera.Score);
        }

        [Fact]
        public void HousingTypeUsesItsOwnMarketData()
        {
            var zona = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageRent = 1_500_000, AverageExpenses = 100_000, AverageSalePriceUsd = 200_000,
                MarketByPropertyType = new Dictionary<string, HousingMarket>
                {
                    [HousingTypes.Apartment] = new(700_000, 5, null, 90_000, 8),
                },
            };
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var compra = motor.Calculate(zona, new ScoringRequest(criterios, [], null, HousingOperations.Purchase, 95_000, HousingTypes.Apartment));
            var alquiler = motor.Calculate(zona, new ScoringRequest(criterios, [], 750_000, HousingType: HousingTypes.Apartment));
            var casas = motor.Calculate(zona, new ScoringRequest(criterios, [], null, HousingOperations.Purchase, 95_000, HousingTypes.House));

            Assert.True(compra.Limits.Single().IsSatisfied);
            Assert.Contains("Precio promedio de departamentos US$ 90.000", compra.Limits.Single().Details);
            // Without apartment-specific expenses, use the zone average: 700,000 + 100,000 exceeds 750,000.
            Assert.False(alquiler.Limits.Single().IsSatisfied);
            Assert.Contains("Alquiler de departamentos $ 700.000 + expensas $ 100.000", alquiler.Limits.Single().Details);
            Assert.Null(casas.Limits.Single().IsSatisfied);
            Assert.Equal("Sin datos de venta de casas para esta zona", casas.Limits.Single().Details);
        }

        [Fact]
        public void HousesAndPhsExcludeBuildingExpenses()
        {
            var zona = new ZoneProfile
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
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var casa = motor.Calculate(zona, new ScoringRequest(criterios, [], 750_000, HousingType: HousingTypes.House));
            var ph = motor.Calculate(zona, new ScoringRequest(criterios, [], 1, HousingOperations.Purchase, 120_000, HousingTypes.Ph));

            Assert.True(casa.Limits.Single().IsSatisfied);
            Assert.Contains("Alquiler de casas $ 700.000 + expensas $ 0 = $ 700.000 por mes", casa.Limits.Single().Details);
            var expensasPh = ph.Limits.Single(limite => limite.Type == ScoringEngine.ExpensesLimit);
            Assert.True(expensasPh.IsSatisfied);
            Assert.Equal(0, zona.GetMarket(HousingTypes.Ph).AverageExpenses);
        }

        [Fact]
        public void AreaUsesPricePerSquareMeterWhenAvailable()
        {
            // Similar to Ramos Mejia: the average apartment price (US$ 95,354) covers about 66 m²; 34 m² costs much less.
            var zona = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageExpenses = 100_000,
                MarketByPropertyType = new Dictionary<string, HousingMarket>
                {
                    [HousingTypes.Apartment] = new(624_000, 58, null, 95_354, 59, RentPricePerM2: 13_855, SalePricePerM2Usd: 1_714),
                },
            };
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var compra = motor.Calculate(zona, new ScoringRequest(
                criterios, [], null, HousingOperations.Purchase, 60_000, HousingTypes.Apartment, AreaM2: 34)).Limits.Single();
            var alquiler = motor.Calculate(zona, new ScoringRequest(
                criterios, [], 600_000, HousingType: HousingTypes.Apartment, AreaM2: 34)).Limits.Single();
            var sinSuperficie = motor.Calculate(zona, new ScoringRequest(
                criterios, [], null, HousingOperations.Purchase, 60_000, HousingTypes.Apartment)).Limits.Single();

            Assert.True(compra.IsSatisfied);
            Assert.Equal("Precio estimado de departamentos para 34 m² US$ 58.276 (US$ 1.714/m²): dentro de tu tope de compra de US$ 60.000", compra.Details);
            // 13,855 x 34 = 471,070 rent + 100,000 in building expenses.
            Assert.True(alquiler.IsSatisfied);
            Assert.Contains("Alquiler estimado de departamentos para 34 m² $ 471.070 + expensas $ 100.000 = $ 571.070 por mes", alquiler.Details);
            Assert.False(sinSuperficie.IsSatisfied);
        }

        [Fact]
        public void AreaFallsBackToZoneAverageWithoutPricePerSquareMeter()
        {
            var zona = new ZoneProfile
            {
                Slug = "zona", Name = "zona", Level = 9, AreaKm2 = 2, Population = 1000, SchoolCount = 10,
                AverageSalePriceUsd = 90_000,
            };
            var motor = new ScoringEngine([zona]);

            var limite = motor.Calculate(zona, new ScoringRequest(
                new Dictionary<string, string> { [ScoringEngine.Education] = "media" }, [], null, HousingOperations.Purchase, 100_000, AreaM2: 34))
                .Limits.Single();

            Assert.Equal("Precio promedio US$ 90.000: dentro de tu tope de compra de US$ 100.000", limite.Details);
        }

        [Fact]
        public void MissingRentDataDoesNotAffectTheScore()
        {
            var zona = CreateZoneProfile("zona");
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Education] = "media" };

            var conTope = motor.Calculate(zona, CreateScoringRequest(criterios, 800_000));

            Assert.Equal(motor.Calculate(zona, CreateScoringRequest(criterios)).Score, conTope.Score);
            var limite = conTope.Limits.Single();
            Assert.Equal(ScoringEngine.RentLimit, limite.Type);
            Assert.Null(limite.IsSatisfied);
        }

        [Fact]
        public void LargeZonesAreComparedWithEachOther()
        {
            // The district has more crimes than any neighborhood, but fewer than the other district.
            var partido = CreateZoneProfile("partido", delitos: 200, nivel: 5);
            var motor = new ScoringEngine([partido, CreateZoneProfile("otro-partido", delitos: 300, nivel: 5), CreateZoneProfile("barrio", delitos: 10)]);

            var seguridad = motor.Calculate(partido, CreateScoringRequest(new() { [ScoringEngine.Safety] = "media" })).Components.Single();

            Assert.Equal(75, seguridad.Score);
        }

        [Fact]
        public void PlacesAddAScoringCriterionAndFlagLongTrips()
        {
            var zona = CreateZoneProfile("zona");
            var motor = new ScoringEngine([zona]);
            var lugares = new List<UserPlace>
            {
                new("Trabajo", -34.6, -58.4, "Muy importante", 45),
                new("Club", -34.6, -58.5, "Baja", 30),
                new("Sin ubicacion", null, null, "Alta", 30),
            };

            var resultado = motor.Calculate(zona, CreateScoringRequest([], lugares: lugares),
                [new TravelTime(30, false), new TravelTime(50, false), null]);

            var componente = Assert.Single(resultado.Components);
            Assert.Equal(ScoringEngine.Places, componente.Criterion);
            // Work scores 87 (weight 4); Club is 67% over its limit and scores 6 (weight 1).
            Assert.Equal(71, componente.Score);
            var limite = Assert.Single(resultado.Limits);
            Assert.False(limite.IsSatisfied);
            Assert.Contains("Club", limite.Details);
        }

        [Fact]
        public void PreservesAllPlaceDistancesWithoutChangingScores()
        {
            var zona = CreateZoneProfile("recoleta");
            var motor = new ScoringEngine([zona]);
            var pedido = CreateScoringRequest([], lugares:
            [
                new("Trabajo", -34.6, -58.4, "Alta", 45),
                new("Facultad", -34.5, -58.3, "Media", 45),
            ]);

            var conDistancias = motor.Calculate(zona, pedido,
                [new TravelTime(25, false, 8.75), new TravelTime(30, false, 12.5)]);
            var sinDistancias = motor.Calculate(zona, pedido,
                [new TravelTime(25, false), new TravelTime(30, false)]);

            Assert.Equal(new double?[] { 8.75, 12.5 }, conDistancias.Places.Select(l => l.DistanceKm));
            Assert.Equal(sinDistancias.Score, conDistancias.Score);
        }

        [Fact]
        public void NoPlacesReturnsAnEmptyList()
        {
            var zona = CreateZoneProfile("recoleta");

            Assert.Empty(new ScoringEngine([zona]).Calculate(zona, CreateScoringRequest([])).Places);
        }

        [Fact]
        public void UnavailableTripDoesNotAffectTheScore()
        {
            var zona = CreateZoneProfile("zona");
            var motor = new ScoringEngine([zona]);
            var lugares = new List<UserPlace>
            {
                new("Trabajo", -34.6, -58.4, "Alta", 45),
                new("Facultad", -34.6, -58.5, "Alta", 45),
            };

            var resultado = motor.Calculate(zona, CreateScoringRequest([], lugares: lugares), [new TravelTime(45, false), new TravelTime(null, false)]);
            var soloTrabajo = motor.Calculate(zona, CreateScoringRequest([], lugares: [lugares[0]]), [new TravelTime(45, false)]);

            Assert.Equal(soloTrabajo.Score, resultado.Score);
            Assert.Null(resultado.Places[1].Score);
            Assert.Contains(resultado.Limits, l => l.IsSatisfied is null && l.Details.Contains("Facultad"));
        }
    }
}
