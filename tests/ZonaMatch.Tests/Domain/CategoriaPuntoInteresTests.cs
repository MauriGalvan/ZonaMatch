using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class CategoriaPuntoInteresTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" , ")]
        public void Lista_vacia_devuelve_todas_las_categorias(string? lista)
        {
            var ok = CategoriaPuntoInteres.TryParseLista(lista, out var categorias, out _);

            Assert.True(ok);
            Assert.Equal(CategoriaPuntoInteres.Todas, categorias);
        }

        [Fact]
        public void Normaliza_mayusculas_espacios_y_repetidos()
        {
            var ok = CategoriaPuntoInteres.TryParseLista(" Transporte, salud,transporte ", out var categorias, out _);

            Assert.True(ok);
            Assert.Equal([CategoriaPuntoInteres.Transporte, CategoriaPuntoInteres.Salud], categorias);
        }

        [Fact]
        public void Categoria_desconocida_falla_y_la_nombra()
        {
            var ok = CategoriaPuntoInteres.TryParseLista("salud,boliches", out var categorias, out var error);

            Assert.False(ok);
            Assert.Empty(categorias);
            Assert.Contains("boliches", error);
        }
    }
}
