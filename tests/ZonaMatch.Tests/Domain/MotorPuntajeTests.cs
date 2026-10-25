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
        [InlineData(0.0, 50.0)]
        [InlineData(0.1, 40.0)]
        [InlineData(0.2, 30.0)]
        [InlineData(0.4, 11.0)]
        [InlineData(1.0, 0.5)]
        public void Pasarse_un_poco_descuenta_un_punto_por_ciento_y_mucho_cae_exponencial(double exceso, double esperado)
        {
            Assert.Equal(esperado, MotorPuntaje.Penalizar(50, exceso), 1);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(45, 80)]
        [InlineData(54, 60)]
        [InlineData(63, 22)]
        [InlineData(90, 1)]
        public void Viaje_vale_80_en_el_maximo_y_despues_se_penaliza_como_un_limite(int minutos, int esperado)
        {
            Assert.Equal(esperado, MotorPuntaje.PuntajeViaje(minutos, 45));
        }

        [Fact]
        public void Viaje_sin_recorrido_no_tiene_puntaje()
        {
            Assert.Null(MotorPuntaje.PuntajeViaje(null, 45));
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
        public void La_prioridad_alta_pesa_nueve_veces_la_baja()
        {
            // Mejor en seguridad (percentil 83) y peor en educacion (percentil 17)
            var zona = Zona("zona", delitos: 10, escuelas: 1);
            var motor = new MotorPuntaje([zona, Zona("b", delitos: 50, escuelas: 20), Zona("c", delitos: 90, escuelas: 30)]);

            var resultado = motor.Calcular(zona, Pedido(new() { [MotorPuntaje.Seguridad] = "alta", [MotorPuntaje.Educacion] = "baja" }));

            Assert.Equal(76, resultado.Puntaje); // (83 * 9 + 17 * 1) / 10
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
        public void Pasarse_del_tope_de_alquiler_descuenta_poco_si_es_poco_y_casi_todo_si_es_mucho()
        {
            // Todas tienen la misma cantidad de escuelas: sin tope, cada una vale 50
            var poco = Zona("poco", alquiler: 1_100_000);
            var limite = Zona("limite", alquiler: 1_200_000);
            var mucho = Zona("mucho", alquiler: 1_500_000);
            var doble = Zona("doble", alquiler: 2_000_000);
            var barata = Zona("barata", alquiler: 500_000);
            var motor = new MotorPuntaje([poco, limite, mucho, doble, barata]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            int? ConTope(PerfilZona z) => motor.Calcular(z, Pedido(criterios, 1_000_000)).Puntaje;

            Assert.Equal(50, motor.Calcular(poco, Pedido(criterios)).Puntaje);
            Assert.Equal(40, ConTope(poco));
            Assert.Equal(30, ConTope(limite));
            Assert.Equal(7, ConTope(mucho));
            Assert.Equal(1, ConTope(doble));
            Assert.Equal(50, ConTope(barata));
            Assert.True(motor.Calcular(barata, Pedido(criterios, 1_000_000)).Limites.Single().Cumple);
            Assert.Contains("en un 50 %", motor.Calcular(mucho, Pedido(criterios, 1_000_000)).Limites.Single().Detalle);
        }

        [Fact]
        public void Sin_dato_de_alquiler_el_limite_no_suma_ni_resta()
        {
            var zona = Zona("zona");
            var motor = new MotorPuntaje([zona]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            var conTope = motor.Calcular(zona, Pedido(criterios, 800_000));

            Assert.Equal(motor.Calcular(zona, Pedido(criterios)).Puntaje, conTope.Puntaje);
            var limite = conTope.Limites.Single();
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
            // Trabajo 87 (peso 4) y Club, un 67 % pasado de su maximo, 6 (peso 1)
            Assert.Equal(71, componente.Puntaje);
            var limite = Assert.Single(resultado.Limites);
            Assert.False(limite.Cumple);
            Assert.Contains("Club", limite.Detalle);
        }

        [Fact]
        public void Un_viaje_que_no_se_pudo_calcular_no_suma_ni_resta()
        {
            var zona = Zona("zona");
            var motor = new MotorPuntaje([zona]);
            var lugares = new List<LugarDelUsuario>
            {
                new("Trabajo", -34.6, -58.4, "Alta", 45),
                new("Facultad", -34.6, -58.5, "Alta", 45),
            };

            var resultado = motor.Calcular(zona, Pedido([], lugares: lugares), [new TiempoViaje(45, false), new TiempoViaje(null, false)]);
            var soloTrabajo = motor.Calcular(zona, Pedido([], lugares: [lugares[0]]), [new TiempoViaje(45, false)]);

            Assert.Equal(soloTrabajo.Puntaje, resultado.Puntaje);
            Assert.Null(resultado.Lugares[1].Puntaje);
            Assert.Contains(resultado.Limites, l => l.Cumple is null && l.Detalle.Contains("Facultad"));
        }
    }
}
