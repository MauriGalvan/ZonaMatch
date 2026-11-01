using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.Domain
{
    public class DistribucionTests
    {
        // 100 a 200 en pasos de 10: cada decimo de los avisos sube 10
        private static readonly decimal[] Deciles = [100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200];

        [Theory]
        [InlineData(90, 0)]
        [InlineData(100, 0)]
        [InlineData(145, 45)]
        [InlineData(150, 50)]
        [InlineData(200, 100)]
        [InlineData(250, 100)]
        public void Porcentaje_de_publicaciones_que_entran_en_el_tope(decimal tope, int esperado)
        {
            Assert.Equal(esperado, Distribucion.PorcentajeHasta(Deciles, tope));
        }

        [Fact]
        public void Con_distribucion_el_tope_se_compara_con_el_precio_medio_y_muestra_el_rango()
        {
            var zona = new PerfilZona
            {
                Slug = "zona", Nombre = "zona", Nivel = 9, SuperficieKm2 = 2, Poblacion = 1000, Escuelas = 10,
                VentaPromedioUsd = 160_000, VentaDecilesUsd = [.. Deciles.Select(d => d * 1000)],
            };
            var motor = new MotorPuntaje([zona]);
            var criterios = new Dictionary<string, string> { [MotorPuntaje.Educacion] = "media" };

            // El promedio (160.000) supera el tope, pero entra la mitad de las publicaciones: cumple y resta 10 puntos
            var limite = motor.Calcular(zona, new PedidoPuntaje(criterios, [], null, Operaciones.Compra, 150_000)).Limites.Single();

            Assert.True(limite.Cumple);
            Assert.Equal(40, motor.Calcular(zona, new PedidoPuntaje(criterios, [], null, Operaciones.Compra, 150_000)).Puntaje);
            Assert.False(motor.Calcular(zona, new PedidoPuntaje(criterios, [], null, Operaciones.Compra, 90_000)).Limites.Single().Cumple);
            Assert.Equal(
                "Precio: de US$ 100.000 a US$ 200.000, precio medio US$ 150.000: entra el 50 % de las publicaciones en tu tope de compra de US$ 150.000",
                limite.Detalle);
        }
    }
}
