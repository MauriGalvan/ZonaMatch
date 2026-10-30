using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.Domain
{
    // Prices by number of rooms (app.zone_market_rooms) in the zone profile and in the score limits
    public class RoomsMarketTests
    {
        private static readonly Dictionary<string, string> Criteria = new() { [MotorPuntaje.Educacion] = "media" };

        // Like Palermo: apartments for rent average $ 1.289.000, but a 2-room one is about $ 1.229.000
        private static PerfilZona Zone() => new()
        {
            Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
            ExpensasPromedio = 280_000,
            MercadoPorTipo = new Dictionary<string, MercadoVivienda>
            {
                [TiposVivienda.Departamento] = new(1_289_000, 3_413, 291_000, 258_200, 11_967, AlquilerM2: 25_259, VentaM2Usd: 3_910),
            },
            MercadoPorAmbientes = new Dictionary<(string Tipo, int Ambientes), MercadoVivienda>
            {
                [(TiposVivienda.Departamento, 2)] = new(1_229_000, 1_353, 250_000, 170_547, 3_560, AlquilerM2: 25_523, VentaM2Usd: 3_738),
                [(TiposVivienda.Departamento, 5)] = new(5_097_000, 104, null, null, null, AlquilerM2: 28_599),
                [(TiposVivienda.Todos, 1)] = new(872_000, 1_029, null, 120_733, 2_785),
            },
        };

        [Fact]
        public void Mercado_WhenRoomsHaveData_UsesThePricesOfThatSize()
        {
            var market = Zone().Mercado(TiposVivienda.Departamento, 2);

            Assert.Equal(1_229_000, market.AlquilerPromedio);
            Assert.Equal(250_000, market.ExpensasPromedio);
            Assert.Equal(170_547, market.VentaPromedioUsd);
            Assert.Equal(2, market.AmbientesAlquiler);
            Assert.Equal(2, market.AmbientesVenta);
        }

        [Fact]
        public void Mercado_WhenOneOperationLacksRoomsData_KeepsTheAverageOfEverySizeForIt()
        {
            // 5 or more rooms: rent of its own, no sale data; expenses fall back to the type's
            var market = Zone().Mercado(TiposVivienda.Departamento, 7);

            Assert.Equal(5_097_000, market.AlquilerPromedio);
            Assert.Equal(5, market.AmbientesAlquiler);
            Assert.Equal(291_000, market.ExpensasPromedio);
            Assert.Equal(258_200, market.VentaPromedioUsd);
            Assert.Null(market.AmbientesVenta);
        }

        [Fact]
        public void Mercado_WhenSizeIsMissing_UsesTheAverageOfEverySize()
        {
            var market = Zone().Mercado(TiposVivienda.Departamento, 3);

            Assert.Equal(1_289_000, market.AlquilerPromedio);
            Assert.Null(market.AmbientesAlquiler);
        }

        [Fact]
        public void Mercado_WithoutPropertyType_UsesTheRoomsOfEveryType()
        {
            var market = Zone().Mercado(null, 1);

            Assert.Equal(872_000, market.AlquilerPromedio);
            Assert.Equal(1, market.AmbientesAlquiler);
        }

        [Fact]
        public void Calcular_WithRooms_ComparesTheBudgetWithThatSize()
        {
            var zone = Zone();
            var limit = new MotorPuntaje([zone]).Calcular(zone, new PedidoPuntaje(
                Criteria, [], 1_500_000, TipoVivienda: TiposVivienda.Departamento, Ambientes: 2)).Limites.Single();

            // 1.229.000 of rent + 250.000 of expenses
            Assert.True(limit.Cumple);
            Assert.Equal(
                "Alquiler de departamentos de 2 ambientes $ 1.229.000 + expensas $ 250.000 = $ 1.479.000 por mes: dentro de tu tope de $ 1.500.000",
                limit.Detalle);
        }

        [Fact]
        public void Calcular_WithRoomsAndSurface_EstimatesWithThePricePerM2OfThatSize()
        {
            var zone = Zone();
            var limit = new MotorPuntaje([zone]).Calcular(zone, new PedidoPuntaje(
                Criteria, [], null, Operaciones.Compra, 150_000, TiposVivienda.Departamento, SuperficieM2: 40, Ambientes: 2))
                .Limites.Single();

            // 3.738 x 40 = 149.520
            Assert.True(limit.Cumple);
            Assert.StartsWith("Precio estimado de departamentos de 2 ambientes para 40 m² US$ 149.520 (US$ 3.738/m²)", limit.Detalle);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData(1, " de 1 ambiente")]
        [InlineData(3, " de 3 ambientes")]
        [InlineData(5, " de 5 ambientes o más")]
        public void Sufijo_NamesTheRoomGroup(int? rooms, string expected)
        {
            Assert.Equal(expected, Ambientes.Sufijo(rooms));
        }
    }
}
