using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class ScoringEngineTests
    {
        private static ZoneProfile CreateZoneProfile(string slug, int delitos = 30, int escuelas = 10, int nivel = 9, decimal? alquiler = null, decimal? expensas = null) => new()
        {
            Slug = slug,
            Nombre = slug,
            Nivel = nivel,
            SuperficieKm2 = 2,
            Poblacion = 1000,
            Delitos = delitos,
            Escuelas = escuelas,
            AlquilerPromedio = alquiler,
            PropiedadesDisponibles = alquiler is null ? null : 10,
            ExpensasPromedio = expensas,
        };

        private static ScoringRequest CreateScoringRequest(Dictionary<string, string> criterios, decimal? alquilerMaximo = null, List<UserPlace>? lugares = null) =>
            new(criterios, lugares ?? [], alquilerMaximo);

        [Fact]
        public void PercentileCountsTiesAsHalf()
        {
            double[] valores = [1, 2, 2, 3];

            Assert.Equal(50, ScoringEngine.Percentil(valores, 2));
            Assert.Equal(13, ScoringEngine.Percentil(valores, 1));
            Assert.Equal(100, ScoringEngine.Percentil(valores, 4));
        }

        [Theory]
        [InlineData(0.0, 50.0)]
        [InlineData(0.1, 40.0)]
        [InlineData(0.2, 30.0)]
        [InlineData(0.4, 11.0)]
        [InlineData(1.0, 0.5)]
        public void SmallOveragesDeductOnePointPerPercentAndLargeOveragesDecayExponentially(double exceso, double esperado)
        {
            Assert.Equal(esperado, ScoringEngine.Penalizar(50, exceso), 1);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(45, 80)]
        [InlineData(54, 60)]
        [InlineData(63, 22)]
        [InlineData(90, 1)]
        public void TravelScoreIs80AtTheLimitAndPenalizedBeyondIt(int minutos, int esperado)
        {
            Assert.Equal(esperado, ScoringEngine.PuntajeViaje(minutos, 45));
        }

        [Fact]
        public void TravelWithoutRouteHasNoScore()
        {
            Assert.Null(ScoringEngine.PuntajeViaje(null, 45));
        }

        [Fact]
        public void SafetyRewardsFewerCrimes()
        {
            var segura = CreateZoneProfile("segura", delitos: 10);
            var insegura = CreateZoneProfile("insegura", delitos: 90);
            var motor = new ScoringEngine([segura, insegura, CreateZoneProfile("media", delitos: 50)]);
            var pedido = CreateScoringRequest(new() { [ScoringEngine.Seguridad] = "media" });

            Assert.True(motor.Calcular(segura, pedido).Puntaje > motor.Calcular(insegura, pedido).Puntaje);
        }

        [Fact]
        public void HighPriorityWeighsNineTimesMoreThanLowPriority()
        {
            // Better on safety (83rd percentile) and worse on education (17th percentile).
            var zona = CreateZoneProfile("zona", delitos: 10, escuelas: 1);
            var motor = new ScoringEngine([zona, CreateZoneProfile("b", delitos: 50, escuelas: 20), CreateZoneProfile("c", delitos: 90, escuelas: 30)]);

            var resultado = motor.Calcular(zona, CreateScoringRequest(new() { [ScoringEngine.Seguridad] = "alta", [ScoringEngine.Educacion] = "baja" }));

            Assert.Equal(76, resultado.Puntaje); // (83 * 9 + 17 * 1) / 10
        }

        [Fact]
        public void MissingCriterionDataIsIgnoredAndNoDataReturnsNullScore()
        {
            var sinDelitos = new ZoneProfile { Slug = "conurbano", Nombre = "Conurbano", Nivel = 8, Poblacion = 1000, SuperficieKm2 = 2, Escuelas = 5 };
            var motor = new ScoringEngine([sinDelitos, CreateZoneProfile("caba")]);

            var soloSeguridad = motor.Calcular(sinDelitos, CreateScoringRequest(new() { [ScoringEngine.Seguridad] = "alta" }));
            var conEducacion = motor.Calcular(sinDelitos, CreateScoringRequest(new() { [ScoringEngine.Seguridad] = "alta", [ScoringEngine.Educacion] = "media" }));

            Assert.Null(soloSeguridad.Puntaje);
            Assert.Null(soloSeguridad.Componentes.Single().Puntaje);
            Assert.NotNull(conEducacion.Puntaje);
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
            var criterios = new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" };

            int? ConTope(ZoneProfile z) => motor.Calcular(z, CreateScoringRequest(criterios, 1_000_000)).Puntaje;

            Assert.Equal(50, motor.Calcular(poco, CreateScoringRequest(criterios)).Puntaje);
            Assert.Equal(40, ConTope(poco));
            Assert.Equal(30, ConTope(limite));
            Assert.Equal(7, ConTope(mucho));
            Assert.Equal(1, ConTope(doble));
            Assert.Equal(50, ConTope(barata));
            Assert.True(motor.Calcular(barata, CreateScoringRequest(criterios, 1_000_000)).Limites.Single().Cumple);
            Assert.Contains("en un 50 %", motor.Calcular(mucho, CreateScoringRequest(criterios, 1_000_000)).Limites.Single().Detalle);
        }

        [Fact]
        public void BuildingExpensesCountTowardTheMonthlyLimit()
        {
            // 900,000 rent is within the 1,000,000 limit, but adding 200,000 in expenses exceeds it by 10%.
            var conExpensas = CreateZoneProfile("con-expensas", alquiler: 900_000, expensas: 200_000);
            var sinExpensas = CreateZoneProfile("sin-expensas", alquiler: 900_000);
            var motor = new ScoringEngine([conExpensas, sinExpensas]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" };

            var limite = motor.Calcular(conExpensas, CreateScoringRequest(criterios, 1_000_000)).Limites.Single();

            Assert.False(limite.Cumple);
            Assert.Contains("= $ 1.100.000 por mes", limite.Detalle);
            Assert.Equal(40, motor.Calcular(conExpensas, CreateScoringRequest(criterios, 1_000_000)).Puntaje);
            Assert.True(motor.Calcular(sinExpensas, CreateScoringRequest(criterios, 1_000_000)).Limites.Single().Cumple);
        }

        [Fact]
        public void BuyersComparePurchasePriceAndExpensesAgainstTheirMonthlyLimit()
        {
            var zona = new ZoneProfile
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                AlquilerPromedio = 900_000, ExpensasPromedio = 200_000, VentaPromedioUsd = 120_000,
            };
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" };

            var dentro = motor.Calcular(zona, new ScoringRequest(criterios, [], 250_000, HousingOperations.Purchase, 150_000));
            var fuera = motor.Calcular(zona, new ScoringRequest(criterios, [], 250_000, HousingOperations.Purchase, 100_000));

            // Rent does not count; a buyer's monthly housing expense is the building expenses.
            Assert.Equal([ScoringEngine.LimiteCompra, ScoringEngine.LimiteExpensas], dentro.Limites.Select(l => l.Tipo));
            Assert.All(dentro.Limites, l => Assert.True(l.Cumple));
            Assert.Equal(50, dentro.Puntaje);
            Assert.False(fuera.Limites[0].Cumple);
            Assert.Contains("Precio promedio US$ 120.000: supera tu tope de compra de US$ 100.000 en un 20 %", fuera.Limites[0].Detalle);
            Assert.Equal(30, fuera.Puntaje);
        }

        [Fact]
        public void HousingTypeUsesItsOwnMarketData()
        {
            var zona = new ZoneProfile
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                AlquilerPromedio = 1_500_000, ExpensasPromedio = 100_000, VentaPromedioUsd = 200_000,
                MercadoPorTipo = new Dictionary<string, HousingMarket>
                {
                    [HousingTypes.Apartment] = new(700_000, 5, null, 90_000, 8),
                },
            };
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" };

            var compra = motor.Calcular(zona, new ScoringRequest(criterios, [], null, HousingOperations.Purchase, 95_000, HousingTypes.Apartment));
            var alquiler = motor.Calcular(zona, new ScoringRequest(criterios, [], 750_000, TipoVivienda: HousingTypes.Apartment));
            var casas = motor.Calcular(zona, new ScoringRequest(criterios, [], null, HousingOperations.Purchase, 95_000, HousingTypes.House));

            Assert.True(compra.Limites.Single().Cumple);
            Assert.Contains("Precio promedio de departamentos US$ 90.000", compra.Limites.Single().Detalle);
            // Without apartment-specific expenses, use the zone average: 700,000 + 100,000 exceeds 750,000.
            Assert.False(alquiler.Limites.Single().Cumple);
            Assert.Contains("Alquiler de departamentos $ 700.000 + expensas $ 100.000", alquiler.Limites.Single().Detalle);
            Assert.Null(casas.Limites.Single().Cumple);
            Assert.Equal("Sin datos de venta de casas para esta zona", casas.Limites.Single().Detalle);
        }

        [Fact]
        public void HousesAndPhsExcludeBuildingExpenses()
        {
            var zona = new ZoneProfile
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                ExpensasPromedio = 100_000,
                MercadoPorTipo = new Dictionary<string, HousingMarket>
                {
                    // The house includes gated-community expenses, which are excluded.
                    [HousingTypes.House] = new(700_000, 5, 570_000, 150_000, 8),
                    [HousingTypes.Ph] = new(600_000, 5, null, 110_000, 8),
                },
            };
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" };

            var casa = motor.Calcular(zona, new ScoringRequest(criterios, [], 750_000, TipoVivienda: HousingTypes.House));
            var ph = motor.Calcular(zona, new ScoringRequest(criterios, [], 1, HousingOperations.Purchase, 120_000, HousingTypes.Ph));

            Assert.True(casa.Limites.Single().Cumple);
            Assert.Contains("Alquiler de casas $ 700.000 + expensas $ 0 = $ 700.000 por mes", casa.Limites.Single().Detalle);
            var expensasPh = ph.Limites.Single(limite => limite.Tipo == ScoringEngine.LimiteExpensas);
            Assert.True(expensasPh.Cumple);
            Assert.Equal(0, zona.GetMarket(HousingTypes.Ph).ExpensasPromedio);
        }

        [Fact]
        public void AreaUsesPricePerSquareMeterWhenAvailable()
        {
            // Similar to Ramos Mejia: the average apartment price (US$ 95,354) covers about 66 m²; 34 m² costs much less.
            var zona = new ZoneProfile
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                ExpensasPromedio = 100_000,
                MercadoPorTipo = new Dictionary<string, HousingMarket>
                {
                    [HousingTypes.Apartment] = new(624_000, 58, null, 95_354, 59, AlquilerM2: 13_855, VentaM2Usd: 1_714),
                },
            };
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" };

            var compra = motor.Calcular(zona, new ScoringRequest(
                criterios, [], null, HousingOperations.Purchase, 60_000, HousingTypes.Apartment, SuperficieM2: 34)).Limites.Single();
            var alquiler = motor.Calcular(zona, new ScoringRequest(
                criterios, [], 600_000, TipoVivienda: HousingTypes.Apartment, SuperficieM2: 34)).Limites.Single();
            var sinSuperficie = motor.Calcular(zona, new ScoringRequest(
                criterios, [], null, HousingOperations.Purchase, 60_000, HousingTypes.Apartment)).Limites.Single();

            Assert.True(compra.Cumple);
            Assert.Equal("Precio estimado de departamentos para 34 m² US$ 58.276 (US$ 1.714/m²): dentro de tu tope de compra de US$ 60.000", compra.Detalle);
            // 13,855 x 34 = 471,070 rent + 100,000 in building expenses.
            Assert.True(alquiler.Cumple);
            Assert.Contains("Alquiler estimado de departamentos para 34 m² $ 471.070 + expensas $ 100.000 = $ 571.070 por mes", alquiler.Detalle);
            Assert.False(sinSuperficie.Cumple);
        }

        [Fact]
        public void AreaFallsBackToZoneAverageWithoutPricePerSquareMeter()
        {
            var zona = new ZoneProfile
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                VentaPromedioUsd = 90_000,
            };
            var motor = new ScoringEngine([zona]);

            var limite = motor.Calcular(zona, new ScoringRequest(
                new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" }, [], null, HousingOperations.Purchase, 100_000, SuperficieM2: 34))
                .Limites.Single();

            Assert.Equal("Precio promedio US$ 90.000: dentro de tu tope de compra de US$ 100.000", limite.Detalle);
        }

        [Fact]
        public void MissingRentDataDoesNotAffectTheScore()
        {
            var zona = CreateZoneProfile("zona");
            var motor = new ScoringEngine([zona]);
            var criterios = new Dictionary<string, string> { [ScoringEngine.Educacion] = "media" };

            var conTope = motor.Calcular(zona, CreateScoringRequest(criterios, 800_000));

            Assert.Equal(motor.Calcular(zona, CreateScoringRequest(criterios)).Puntaje, conTope.Puntaje);
            var limite = conTope.Limites.Single();
            Assert.Equal(ScoringEngine.LimiteAlquiler, limite.Tipo);
            Assert.Null(limite.Cumple);
        }

        [Fact]
        public void LargeZonesAreComparedWithEachOther()
        {
            // The district has more crimes than any neighborhood, but fewer than the other district.
            var partido = CreateZoneProfile("partido", delitos: 200, nivel: 5);
            var motor = new ScoringEngine([partido, CreateZoneProfile("otro-partido", delitos: 300, nivel: 5), CreateZoneProfile("barrio", delitos: 10)]);

            var seguridad = motor.Calcular(partido, CreateScoringRequest(new() { [ScoringEngine.Seguridad] = "media" })).Componentes.Single();

            Assert.Equal(75, seguridad.Puntaje);
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

            var resultado = motor.Calcular(zona, CreateScoringRequest([], lugares: lugares),
                [new TravelTime(30, false), new TravelTime(50, false), null]);

            var componente = Assert.Single(resultado.Componentes);
            Assert.Equal(ScoringEngine.Lugares, componente.Criterion);
            // Work scores 87 (weight 4); Club is 67% over its limit and scores 6 (weight 1).
            Assert.Equal(71, componente.Puntaje);
            var limite = Assert.Single(resultado.Limites);
            Assert.False(limite.Cumple);
            Assert.Contains("Club", limite.Detalle);
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

            var conDistancias = motor.Calcular(zona, pedido,
                [new TravelTime(25, false, 8.75), new TravelTime(30, false, 12.5)]);
            var sinDistancias = motor.Calcular(zona, pedido,
                [new TravelTime(25, false), new TravelTime(30, false)]);

            Assert.Equal(new double?[] { 8.75, 12.5 }, conDistancias.Lugares.Select(l => l.DistanciaKm));
            Assert.Equal(sinDistancias.Puntaje, conDistancias.Puntaje);
        }

        [Fact]
        public void NoPlacesReturnsAnEmptyList()
        {
            var zona = CreateZoneProfile("recoleta");

            Assert.Empty(new ScoringEngine([zona]).Calcular(zona, CreateScoringRequest([])).Lugares);
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

            var resultado = motor.Calcular(zona, CreateScoringRequest([], lugares: lugares), [new TravelTime(45, false), new TravelTime(null, false)]);
            var soloTrabajo = motor.Calcular(zona, CreateScoringRequest([], lugares: [lugares[0]]), [new TravelTime(45, false)]);

            Assert.Equal(soloTrabajo.Puntaje, resultado.Puntaje);
            Assert.Null(resultado.Lugares[1].Puntaje);
            Assert.Contains(resultado.Limites, l => l.Cumple is null && l.Detalle.Contains("Facultad"));
        }
    }
}
