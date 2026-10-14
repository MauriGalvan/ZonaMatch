using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.Domain
{
    public class MotorPuntajeTests
    {
        private static PerfilZona Zona(string slug, int delitos = 30, int escuelas = 10, int nivel = 9, decimal? alquiler = null) => new()
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
        };

        private static PedidoPuntaje Pedido(Dictionary<string, string> criterios, decimal? alquilerMaximo = null, List<LugarDelUsuario>? lugares = null) =>
            new(criterios, lugares ?? [], alquilerMaximo);

        [Fact]
        public void Percentil_cuenta_los_empates_por_la_mitad()
        {
            double[] valores = [1, 2, 2, 3];

            Assert.Equal(50, MotorPuntaje.Percentil(valores, 2));
            Assert.Equal(13, MotorPuntaje.Percentil(valores, 1));
            Assert.Equal(100, MotorPuntaje.Percentil(valores, 4));
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(45, 80)]
        [InlineData(60, 53)]
        [InlineData(90, 0)]
        [InlineData(200, 0)]
        public void Viaje_vale_80_en_el_maximo_y_0_al_doble(int minutos, int esperado)
        {
            Assert.Equal(esperado, MotorPuntaje.PuntajeViaje(minutos, 45));
        }

        [Fact]
        public void Viaje_sin_recorrido_vale_0()
        {
            Assert.Equal(0, MotorPuntaje.PuntajeViaje(null, 45));
        }

        [Fact]
        public void Seguridad_premia_menos_delitos()
        {
            var segura = Zona("segura", delitos: 10);
            var insegura = Zona("insegura", delitos: 90);
            var motor = new MotorPuntaje([segura, insegura, Zona("media", delitos: 50)]);
            var pedido = Pedido(new() { [MotorPuntaje.Seguridad] = "media" });

            Assert.True(motor.Calcular(segura, pedido).Puntaje > motor.Calcular(insegura, pedido).Puntaje);
        }

        [Fact]
        public void La_prioridad_alta_pesa_el_triple_que_la_baja()
        {
            // Mejor en seguridad (percentil 83) y peor en educacion (percentil 17)
            var zona = Zona("zona", delitos: 10, escuelas: 1);
            var motor = new MotorPuntaje([zona, Zona("b", delitos: 50, escuelas: 20), Zona("c", delitos: 90, escuelas: 30)]);

            var resultado = motor.Calcular(zona, Pedido(new() { [MotorPuntaje.Seguridad] = "alta", [MotorPuntaje.Educacion] = "baja" }));

            Assert.Equal(67, resultado.Puntaje); // (83 * 3 + 17 * 1) / 4
        }

        [Fact]
        public void Un_criterio_sin_dato_no_cuenta_y_sin_ninguno_no_hay_puntaje()
        {
            var sinDelitos = new PerfilZona { Slug = "conurbano", Nombre = "Conurbano", Nivel = 8, Poblacion = 1000, SuperficieKm2 = 2, Escuelas = 5 };
            var motor = new MotorPuntaje([sinDelitos, Zona("caba")]);

            var soloSeguridad = motor.Calcular(sinDelitos, Pedido(new() { [MotorPuntaje.Seguridad] = "alta" }));
            var conEducacion = motor.Calcular(sinDelitos, Pedido(new() { [MotorPuntaje.Seguridad] = "alta", [MotorPuntaje.Educacion] = "media" }));

            Assert.Null(soloSeguridad.Puntaje);
            Assert.Null(soloSeguridad.Componentes.Single().Puntaje);
            Assert.NotNull(conEducacion.Puntaje);
        }

        [Fact]
        public void Pasarse_del_tope_de_alquiler_descuenta_hasta_40_puntos()
        {
            var cara = Zona("cara", alquiler: 2_000_000);
            var poco = Zona("poco", alquiler: 1_100_000);
            var barata = Zona("barata", alquiler: 500_000);
            var motor = new MotorPuntaje([cara, poco, barata]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            int? Descuento(PerfilZona z) => motor.Calcular(z, Pedido(criterios)).Puntaje - motor.Calcular(z, Pedido(criterios, 1_000_000)).Puntaje;

            Assert.Equal(MotorPuntaje.DescuentoMaximoAlquiler, Descuento(cara));
            Assert.Equal(10, Descuento(poco));
            Assert.Equal(0, Descuento(barata));
            Assert.True(motor.Calcular(barata, Pedido(criterios, 1_000_000)).Limites.Single().Cumple);
        }

        [Fact]
        public void Sin_dato_de_alquiler_el_limite_queda_sin_saber()
        {
            var zona = Zona("zona");
            var limite = new MotorPuntaje([zona]).Calcular(zona, Pedido([], 800_000)).Limites.Single();

            Assert.Equal(MotorPuntaje.LimiteAlquiler, limite.Tipo);
            Assert.Null(limite.Cumple);
        }

        [Fact]
        public void Las_zonas_grandes_se_comparan_entre_si()
        {
            // El partido tiene mas delitos que cualquier barrio, pero menos que el otro partido
            var partido = Zona("partido", delitos: 200, nivel: 5);
            var motor = new MotorPuntaje([partido, Zona("otro-partido", delitos: 300, nivel: 5), Zona("barrio", delitos: 10)]);

            var seguridad = motor.Calcular(partido, Pedido(new() { [MotorPuntaje.Seguridad] = "media" })).Componentes.Single();

            Assert.Equal(75, seguridad.Puntaje);
        }

        [Fact]
        public void Los_lugares_suman_un_criterio_y_avisan_los_que_quedan_lejos()
        {
            var zona = Zona("zona");
            var motor = new MotorPuntaje([zona]);
            var lugares = new List<LugarDelUsuario>
            {
                new("Trabajo", -34.6, -58.4, "Muy importante", 45),
                new("Club", -34.6, -58.5, "Baja", 30),
                new("Sin ubicacion", null, null, "Alta", 30),
            };

            var resultado = motor.Calcular(zona, Pedido([], lugares: lugares),
                [new TiempoViaje(30, false), new TiempoViaje(50, false), null]);

            var componente = Assert.Single(resultado.Componentes);
            Assert.Equal(MotorPuntaje.Lugares, componente.Criterio);
            // Trabajo 87 (peso 4) y Club 27 (peso 1)
            Assert.Equal(75, componente.Puntaje);
            var limite = Assert.Single(resultado.Limites);
            Assert.False(limite.Cumple);
            Assert.Contains("Club", limite.Detalle);
        }
    }
}
