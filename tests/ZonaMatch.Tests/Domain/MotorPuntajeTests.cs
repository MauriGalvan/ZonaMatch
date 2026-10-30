using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.Domain
{
    public class MotorPuntajeTests
    {
        private static PerfilZona Zona(string slug, int delitos = 30, int escuelas = 10, int nivel = 9, decimal? alquiler = null, decimal? expensas = null) => new()
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
        public void Las_expensas_cuentan_para_el_tope_mensual()
        {
            // 900.000 de alquiler queda dentro de 1.000.000, pero con 200.000 de expensas lo supera en un 10 %
            var conExpensas = Zona("con-expensas", alquiler: 900_000, expensas: 200_000);
            var sinExpensas = Zona("sin-expensas", alquiler: 900_000);
            var motor = new MotorPuntaje([conExpensas, sinExpensas]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            var limite = motor.Calcular(conExpensas, Pedido(criterios, 1_000_000)).Limites.Single();

            Assert.False(limite.Cumple);
            Assert.Contains("= $ 1.100.000 por mes", limite.Detalle);
            Assert.Equal(40, motor.Calcular(conExpensas, Pedido(criterios, 1_000_000)).Puntaje);
            Assert.True(motor.Calcular(sinExpensas, Pedido(criterios, 1_000_000)).Limites.Single().Cumple);
        }

        [Fact]
        public void Quien_compra_compara_el_precio_en_dolares_y_las_expensas_con_su_gasto_mensual()
        {
            var zona = new PerfilZona
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                AlquilerPromedio = 900_000, ExpensasPromedio = 200_000, VentaPromedioUsd = 120_000,
            };
            var motor = new MotorPuntaje([zona]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            var dentro = motor.Calcular(zona, new PedidoPuntaje(criterios, [], 250_000, Operaciones.Compra, 150_000));
            var fuera = motor.Calcular(zona, new PedidoPuntaje(criterios, [], 250_000, Operaciones.Compra, 100_000));

            // El alquiler no cuenta: el gasto mensual de quien compra son las expensas
            Assert.Equal([MotorPuntaje.LimiteCompra, MotorPuntaje.LimiteExpensas], dentro.Limites.Select(l => l.Tipo));
            Assert.All(dentro.Limites, l => Assert.True(l.Cumple));
            Assert.Equal(50, dentro.Puntaje);
            Assert.False(fuera.Limites[0].Cumple);
            Assert.Contains("Precio promedio US$ 120.000: supera tu tope de compra de US$ 100.000 en un 20 %", fuera.Limites[0].Detalle);
            Assert.Equal(30, fuera.Puntaje);
        }

        [Fact]
        public void El_tipo_de_vivienda_usa_su_propio_mercado()
        {
            var zona = new PerfilZona
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                AlquilerPromedio = 1_500_000, ExpensasPromedio = 100_000, VentaPromedioUsd = 200_000,
                MercadoPorTipo = new Dictionary<string, MercadoVivienda>
                {
                    [TiposVivienda.Departamento] = new(700_000, 5, null, 90_000, 8),
                },
            };
            var motor = new MotorPuntaje([zona]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            var compra = motor.Calcular(zona, new PedidoPuntaje(criterios, [], null, Operaciones.Compra, 95_000, TiposVivienda.Departamento));
            var alquiler = motor.Calcular(zona, new PedidoPuntaje(criterios, [], 750_000, TipoVivienda: TiposVivienda.Departamento));
            var casas = motor.Calcular(zona, new PedidoPuntaje(criterios, [], null, Operaciones.Compra, 95_000, TiposVivienda.Casa));

            Assert.True(compra.Limites.Single().Cumple);
            Assert.Contains("Precio promedio de departamentos US$ 90.000", compra.Limites.Single().Detalle);
            // Sin expensas propias de departamentos, usa las de la zona: 700.000 + 100.000 supera 750.000
            Assert.False(alquiler.Limites.Single().Cumple);
            Assert.Contains("Alquiler de departamentos $ 700.000 + expensas $ 100.000", alquiler.Limites.Single().Detalle);
            Assert.Null(casas.Limites.Single().Cumple);
            Assert.Equal("Sin datos de venta de casas para esta zona", casas.Limites.Single().Detalle);
        }

        [Fact]
        public void Casas_y_PH_van_sin_expensas()
        {
            var zona = new PerfilZona
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                ExpensasPromedio = 100_000,
                MercadoPorTipo = new Dictionary<string, MercadoVivienda>
                {
                    // La casa trae expensas de barrios cerrados: no se usan
                    [TiposVivienda.Casa] = new(700_000, 5, 570_000, 150_000, 8),
                    [TiposVivienda.Ph] = new(600_000, 5, null, 110_000, 8),
                },
            };
            var motor = new MotorPuntaje([zona]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            var casa = motor.Calcular(zona, new PedidoPuntaje(criterios, [], 750_000, TipoVivienda: TiposVivienda.Casa));
            var ph = motor.Calcular(zona, new PedidoPuntaje(criterios, [], 1, Operaciones.Compra, 120_000, TiposVivienda.Ph));

            Assert.True(casa.Limites.Single().Cumple);
            Assert.Contains("Alquiler de casas $ 700.000 por mes (sin expensas)", casa.Limites.Single().Detalle);
            var expensasPh = ph.Limites.Single(limite => limite.Tipo == MotorPuntaje.LimiteExpensas);
            Assert.True(expensasPh.Cumple);
            Assert.Equal(0, zona.Mercado(TiposVivienda.Ph).ExpensasPromedio);
        }

        [Fact]
        public void Con_superficie_los_precios_se_estiman_por_m2()
        {
            // Como Ramos Mejia: el promedio de departamentos (US$ 95.354) es de unos 66 m2; 34 m2 salen bastante menos
            var zona = new PerfilZona
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                ExpensasPromedio = 100_000,
                MercadoPorTipo = new Dictionary<string, MercadoVivienda>
                {
                    [TiposVivienda.Departamento] = new(624_000, 58, null, 95_354, 59, AlquilerM2: 13_855, VentaM2Usd: 1_714),
                },
            };
            var motor = new MotorPuntaje([zona]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            var compra = motor.Calcular(zona, new PedidoPuntaje(
                criterios, [], null, Operaciones.Compra, 60_000, TiposVivienda.Departamento, SuperficieM2: 34)).Limites.Single();
            var alquiler = motor.Calcular(zona, new PedidoPuntaje(
                criterios, [], 600_000, TipoVivienda: TiposVivienda.Departamento, SuperficieM2: 34)).Limites.Single();
            var sinSuperficie = motor.Calcular(zona, new PedidoPuntaje(
                criterios, [], null, Operaciones.Compra, 60_000, TiposVivienda.Departamento)).Limites.Single();

            Assert.True(compra.Cumple);
            Assert.Equal("Precio estimado de departamentos para 34 m² US$ 58.276 (US$ 1.714/m²): dentro de tu tope de compra de US$ 60.000", compra.Detalle);
            // 13.855 x 34 = 471.070 de alquiler + 100.000 de expensas
            Assert.True(alquiler.Cumple);
            Assert.Contains("Alquiler estimado de departamentos para 34 m² $ 471.070 + expensas $ 100.000 = $ 571.070 por mes", alquiler.Detalle);
            Assert.False(sinSuperficie.Cumple);
        }

        [Fact]
        public void Sin_precio_por_m2_la_superficie_usa_el_promedio()
        {
            var zona = new PerfilZona
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                VentaPromedioUsd = 90_000,
            };
            var motor = new MotorPuntaje([zona]);

            var limite = motor.Calcular(zona, new PedidoPuntaje(
                new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" }, [], null, Operaciones.Compra, 100_000, SuperficieM2: 34))
                .Limites.Single();

            Assert.Equal("Precio promedio US$ 90.000: dentro de tu tope de compra de US$ 100.000", limite.Detalle);
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
        public void Conserva_distancia_de_todos_los_lugares_sin_cambiar_el_puntaje()
        {
            var zona = Zona("recoleta");
            var motor = new MotorPuntaje([zona]);
            var pedido = Pedido([], lugares:
            [
                new("Trabajo", -34.6, -58.4, "Alta", 45),
                new("Facultad", -34.5, -58.3, "Media", 45),
            ]);

            var conDistancias = motor.Calcular(zona, pedido,
                [new TiempoViaje(25, false, 8.75), new TiempoViaje(30, false, 12.5)]);
            var sinDistancias = motor.Calcular(zona, pedido,
                [new TiempoViaje(25, false), new TiempoViaje(30, false)]);

            Assert.Equal(new double?[] { 8.75, 12.5 }, conDistancias.Lugares.Select(l => l.DistanciaKm));
            Assert.Equal(sinDistancias.Puntaje, conDistancias.Puntaje);
        }

        [Fact]
        public void Sin_lugares_devuelve_lista_vacia()
        {
            var zona = Zona("recoleta");

            Assert.Empty(new MotorPuntaje([zona]).Calcular(zona, Pedido([])).Lugares);
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
