using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class ResenaTests
    {
        [Theory]
        [InlineData(3, 5, 4, 4, 2, 3.6)]
        [InlineData(5, 5, 5, 5, 5, 5.0)]
        [InlineData(1, 1, 1, 1, 2, 1.2)]
        public void El_puntaje_general_es_el_promedio_de_los_aspectos(
            int seguridad, int transporte, int conectividad, int comercios, int espaciosVerdes, double esperado)
        {
            var resena = new Resena
            {
                PuntajeSeguridad = seguridad,
                PuntajeTransporte = transporte,
                PuntajeConectividad = conectividad,
                PuntajeComercios = comercios,
                PuntajeEspaciosVerdes = espaciosVerdes
            };

            Assert.Equal(esperado, resena.Puntaje);
        }

        [Fact]
        public void El_puntaje_general_sigue_a_los_aspectos_al_editar()
        {
            var resena = new Resena
            {
                PuntajeSeguridad = 4,
                PuntajeTransporte = 4,
                PuntajeConectividad = 4,
                PuntajeComercios = 4,
                PuntajeEspaciosVerdes = 4
            };

            resena.PuntajeSeguridad = 1;

            Assert.Equal(3.4, resena.Puntaje);
        }
    }
}
