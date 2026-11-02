using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class ReviewTests
    {
        [Theory]
        [InlineData(3, 5, 4, 4, 2, 3.6)]
        [InlineData(5, 5, 5, 5, 5, 5.0)]
        [InlineData(1, 1, 1, 1, 2, 1.2)]
        public void El_puntaje_general_es_el_promedio_de_los_aspectos(
            int seguridad, int transporte, int conectividad, int comercios, int espaciosVerdes, double esperado)
        {
            var resena = new Review
            {
                SafetyScore = seguridad,
                TransitScore = transporte,
                ConnectivityScore = conectividad,
                CommerceScore = comercios,
                GreenSpaceScore = espaciosVerdes
            };

            Assert.Equal(esperado, resena.OverallScore);
        }

        [Fact]
        public void El_puntaje_general_sigue_a_los_aspectos_al_editar()
        {
            var resena = new Review
            {
                SafetyScore = 4,
                TransitScore = 4,
                ConnectivityScore = 4,
                CommerceScore = 4,
                GreenSpaceScore = 4
            };

            resena.SafetyScore = 1;

            Assert.Equal(3.4, resena.OverallScore);
        }
    }
}
