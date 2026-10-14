using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class FranjaHorariaTests
    {
        [Fact]
        public void Las_franjas_cubren_el_dia_sin_huecos()
        {
            var franjas = FranjaHoraria.Todas;

            Assert.Equal(0, franjas[0].Desde);
            Assert.Equal(24, franjas[^1].Hasta);
            for (var i = 1; i < franjas.Count; i++)
                Assert.Equal(franjas[i - 1].Hasta, franjas[i].Desde);
        }

        [Fact]
        public void Salidas_por_hora_y_minutos_entre_salidas()
        {
            var manana = FranjaHoraria.Manana; // 3 horas

            Assert.Equal(11.7, manana.SalidasPorHora(35));
            Assert.Equal(5, manana.MinutosEntreSalidas(35));
        }

        [Fact]
        public void Sin_salidas_no_hay_frecuencia()
        {
            Assert.Equal(0, FranjaHoraria.Noche.SalidasPorHora(0));
            Assert.Null(FranjaHoraria.Noche.MinutosEntreSalidas(0));
        }

        [Fact]
        public void Se_busca_por_codigo()
        {
            Assert.Same(FranjaHoraria.Tarde, FranjaHoraria.PorCodigo("tarde"));
            Assert.Null(FranjaHoraria.PorCodigo("siesta"));
        }
    }
}
