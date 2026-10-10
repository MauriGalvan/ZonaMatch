using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class AspectoZonaTests
    {
        [Fact]
        public void Normaliza_mayusculas_repetidos_y_orden()
        {
            var valido = AspectoZona.TryNormalizar([" Transporte", "seguridad", "transporte", ""], out var temas, out _);

            Assert.True(valido);
            Assert.Equal([AspectoZona.Seguridad, AspectoZona.Transporte], temas);
        }

        [Fact]
        public void Sin_temas_es_valido()
        {
            Assert.True(AspectoZona.TryNormalizar(null, out var temas, out _));
            Assert.Empty(temas);
        }

        [Fact]
        public void Tema_desconocido_falla_y_lo_nombra()
        {
            var valido = AspectoZona.TryNormalizar(["seguridad", "ruido"], out var temas, out var error);

            Assert.False(valido);
            Assert.Empty(temas);
            Assert.Contains("ruido", error);
        }
    }
}
