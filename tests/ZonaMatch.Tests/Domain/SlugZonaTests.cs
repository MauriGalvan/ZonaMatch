using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class SlugZonaTests
    {
        [Theory]
        [InlineData("Villa Luro", "villa-luro")]
        [InlineData("Núñez", "nunez")]
        [InlineData("Villa Gral. Mitre", "villa-gral-mitre")]
        [InlineData("  San José (Almirante Brown) ", "san-jose-almirante-brown")]
        [InlineData("Comuna 10", "comuna-10")]
        public void Genera_minusculas_sin_acentos_separadas_por_guiones(string nombre, string esperado)
        {
            Assert.Equal(esperado, SlugZona.Generar(nombre));
        }

        [Theory]
        [InlineData("villa-luro", true)]
        [InlineData("comuna-10", true)]
        [InlineData("Villa-Luro", false)]
        [InlineData("villa--luro", false)]
        [InlineData("-villa", false)]
        [InlineData("villa luro", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Valida_el_formato(string? slug, bool esperado)
        {
            Assert.Equal(esperado, SlugZona.EsValido(slug));
        }
    }
}
