using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class TagPuntoInteresTests
    {
        [Theory]
        [InlineData("Peluquería", "peluqueria")]
        [InlineData("  Casa   de AMIGO ", "casa de amigo")]
        [InlineData("Clínica / obra social", "clinica / obra social")]
        [InlineData("Ñandú", "nandu")]
        public void Normalizar_ignora_mayusculas_tildes_y_espacios_repetidos(string nombre, string esperado)
        {
            Assert.Equal(esperado, TagPuntoInteres.Normalizar(nombre));
        }

        [Fact]
        public void Escrituras_distintas_del_mismo_tag_comparten_nombre_normalizado()
        {
            Assert.Equal(TagPuntoInteres.Normalizar("Peluquería"), TagPuntoInteres.Normalizar(" peluqueria "));
        }

        [Theory]
        [InlineData("  Casa   de amigo ", "Casa de amigo")]
        [InlineData("Trabajo", "Trabajo")]
        [InlineData("   ", "")]
        [InlineData(null, "")]
        public void LimpiarNombre_recorta_y_colapsa_espacios(string? nombre, string esperado)
        {
            Assert.Equal(esperado, TagPuntoInteres.LimpiarNombre(nombre));
        }
    }
}
