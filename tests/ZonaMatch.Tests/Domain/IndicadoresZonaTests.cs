using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class IndicadoresZonaTests
    {
        [Theory]
        [InlineData(907, 1000, 90.7)]
        [InlineData(1, 3, 33.3)]
        [InlineData(0, 10, 0.0)]
        public void El_porcentaje_tiene_un_decimal(int parte, int total, double esperado)
        {
            Assert.Equal(esperado, IndicadoresZona.Porcentaje(parte, total));
        }

        [Fact]
        public void Sin_base_no_hay_porcentaje()
        {
            var zona = new IndicadoresZona { Hogares = 0, HogaresInternet = 0 };

            Assert.Null(zona.PorcentajeHogaresInternet);
            Assert.Null(zona.PorcentajeMenores15);
        }

        [Fact]
        public void La_densidad_es_habitantes_por_km2_redondeada()
        {
            var zona = new IndicadoresZona { Poblacion = 34645, SuperficieKm2 = 2.566 };

            Assert.Equal(13502, zona.DensidadHabitantesKm2);
        }

        [Fact]
        public void La_tasa_de_delitos_es_cada_1000_habitantes()
        {
            var zona = new IndicadoresZona { Poblacion = 34645, Delitos = 1323 };

            Assert.True(zona.TieneDelitos);
            Assert.Equal(38.2, zona.DelitosCada1000Habitantes);
        }

        [Fact]
        public void Sin_delitos_cargados_no_hay_tasa()
        {
            var zona = new IndicadoresZona { Poblacion = 84410, Delitos = null };

            Assert.False(zona.TieneDelitos);
            Assert.Null(zona.DelitosCada1000Habitantes);
        }

        [Fact]
        public void La_edad_se_calcula_sobre_las_personas_con_edad_informada()
        {
            var zona = new IndicadoresZona { Poblacion = 1000, Personas0a14 = 100, Personas15a64 = 600, Personas65yMas = 100 };

            Assert.Equal(12.5, zona.PorcentajeMenores15);
            Assert.Equal(75.0, zona.Porcentaje15a64);
            Assert.Equal(12.5, zona.Porcentaje65yMas);
        }
    }
}
