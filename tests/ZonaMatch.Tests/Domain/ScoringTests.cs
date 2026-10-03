using ZonaMatch.Domain.Scoring;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Domain
{
    public class ZoneScoreCalculatorTests
    {
        private static ZoneCriterionValues Zone(int id, double? security, double? green) =>
            new(id, new Dictionary<string, double?> { ["seguridad"] = security, ["espacios_verdes"] = green });

        [Fact]
        public void Valida_argumentos()
        {
            Assert.Throws<ArgumentNullException>(() => ZoneScoreCalculator.Calculate(null!, []));
            Assert.Throws<ArgumentNullException>(() => ZoneScoreCalculator.Calculate([], null!));
            Assert.Throws<ArgumentException>(() => ZoneScoreCalculator.Calculate([], []));
        }

        [Fact]
        public void Pondera_por_prioridad_y_respeta_la_direccion()
        {
            var criteria = new[]
            {
                new WeightedCriterion("seguridad", Priority.High, CriterionDirection.LowerIsBetter),
                new WeightedCriterion("espacios_verdes", Priority.Low, CriterionDirection.HigherIsBetter),
            };

            var result = ZoneScoreCalculator.Calculate([Zone(1, 30, 2), Zone(2, 10, 0)], criteria);

            Assert.Equal(3, criteria[0].Weight);
            Assert.Equal(1, criteria[1].Weight);
            // zona 2: mejor seguridad (100) y peor verde (40) -> (100*3 + 40) / 4
            Assert.Equal(2, result[0].ZoneId);
            Assert.Equal(85d, result[0].Score);
            Assert.Equal(55d, result[1].Score);
            Assert.Equal(1d, result[0].Coverage);
            Assert.Equal(new CriterionScore("seguridad", 100d), result[0].Breakdown[0]);
        }

        [Fact]
        public void Valores_iguales_puntuan_100_y_desempata_por_id()
        {
            var criteria = new[] { new WeightedCriterion("seguridad", Priority.Medium, CriterionDirection.HigherIsBetter) };

            var result = ZoneScoreCalculator.Calculate([Zone(7, 5, null), Zone(3, 5, null)], criteria);

            Assert.Equal([3, 7], result.Select(score => score.ZoneId));
            Assert.All(result, score => Assert.Equal(100d, score.Score));
        }

        [Fact]
        public void Informa_cobertura_cuando_faltan_datos()
        {
            var criteria = new[]
            {
                new WeightedCriterion("seguridad", Priority.Medium, CriterionDirection.HigherIsBetter),
                new WeightedCriterion("espacios_verdes", Priority.Medium, CriterionDirection.HigherIsBetter),
                new WeightedCriterion("conectividad", Priority.Medium, CriterionDirection.HigherIsBetter),
            };

            var result = ZoneScoreCalculator.Calculate([Zone(1, 4, null), Zone(2, null, null)], criteria);

            var withData = result.Single(score => score.ZoneId == 1);
            var withoutData = result.Single(score => score.ZoneId == 2);
            Assert.Equal(100d, withData.Score);
            Assert.Equal(0.33d, withData.Coverage);
            Assert.Null(withData.Breakdown[2].Score);
            Assert.Equal(0d, withoutData.Score);
            Assert.Equal(0d, withoutData.Coverage);
        }
    }

    public class RestrictionsTests
    {
        [Theory]
        [InlineData(null, RestrictionStatus.Unknown)]
        [InlineData(101d, RestrictionStatus.Exceeds)]
        [InlineData(95d, RestrictionStatus.NearLimit)]
        [InlineData(90d, RestrictionStatus.NearLimit)]
        [InlineData(50d, RestrictionStatus.Complies)]
        public void Evaluate_clasifica_segun_el_limite(double? actual, RestrictionStatus expected)
        {
            Assert.Equal(expected, RestrictionEvaluator.Evaluate(actual, 100));
        }

        [Fact]
        public void Evaluate_exige_limite_positivo()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RestrictionEvaluator.Evaluate(1, 0));
        }

        [Fact]
        public void Combine_promedia_segun_el_peso_de_cada_participante()
        {
            Assert.Equal(82.5d, ProfileCombiner.Combine([(1d, 80d), (1d, 85d)]));
            Assert.Equal(83.3d, ProfileCombiner.Combine([(1d, 80d), (2d, 85d)]));
        }

        [Fact]
        public void Combine_valida_participantes()
        {
            Assert.Throws<ArgumentNullException>(() => ProfileCombiner.Combine(null!));
            Assert.Throws<ArgumentException>(() => ProfileCombiner.Combine([]));
            Assert.Throws<ArgumentException>(() => ProfileCombiner.Combine([(0d, 80d)]));
        }
    }

    public class TravelTimeEstimatorTests
    {
        [Theory]
        [InlineData(TravelMode.Walk, 4.5d)]
        [InlineData(TravelMode.Bike, 14d)]
        [InlineData(TravelMode.Car, 25d)]
        [InlineData(TravelMode.PublicTransport, 18d)]
        public void SpeedKmh_por_modo(TravelMode mode, double expected)
        {
            Assert.Equal(expected, TravelTimeEstimator.SpeedKmh(mode));
        }

        [Fact]
        public void SpeedKmh_rechaza_modos_desconocidos()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TravelTimeEstimator.SpeedKmh((TravelMode)99));
        }

        [Fact]
        public void EstimateMinutes_aplica_desvio_y_velocidad()
        {
            var from = TestGeometry.Point(0, 0);
            var to = TestGeometry.Point(0, 0.0449661); // ~5 km

            Assert.Equal(27.9d, TravelTimeEstimator.EstimateMinutes(from, to, TravelMode.Bike), 1);
            Assert.Throws<ArgumentNullException>(() => TravelTimeEstimator.EstimateMinutes(null!, to, TravelMode.Walk));
            Assert.Throws<ArgumentNullException>(() => TravelTimeEstimator.EstimateMinutes(from, null!, TravelMode.Walk));
        }

        [Fact]
        public void WeeklyMinutes_suma_ida_y_vuelta()
        {
            Assert.Equal(460d, TravelTimeEstimator.WeeklyMinutes([(40d, 5), (10d, 3)]));
            Assert.Throws<ArgumentNullException>(() => TravelTimeEstimator.WeeklyMinutes(null!));
        }
    }
}
