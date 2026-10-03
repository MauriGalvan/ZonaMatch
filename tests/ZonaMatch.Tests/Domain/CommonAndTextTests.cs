using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Domain.Text;

namespace ZonaMatch.Tests.Domain
{
    public class GuardTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NotBlank_rechaza_valores_vacios(string? value)
        {
            var exception = Assert.Throws<ArgumentException>(() => Guard.NotBlank(value, "campo"));
            Assert.Equal("campo", exception.ParamName);
        }

        [Fact]
        public void NotBlank_devuelve_el_valor_sin_espacios()
        {
            Assert.Equal("Haedo", Guard.NotBlank("  Haedo ", "campo"));
        }

        [Fact]
        public void NotNull_rechaza_null_y_devuelve_el_valor()
        {
            Assert.Throws<ArgumentNullException>(() => Guard.NotNull<string>(null, "campo"));
            Assert.Equal("x", Guard.NotNull("x", "campo"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Positive_rechaza_cero_y_negativos(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Positive(value, "campo"));
        }

        [Fact]
        public void Positive_y_NotNegative_aceptan_valores_validos()
        {
            Assert.Equal(4, Guard.Positive(4, "campo"));
            Assert.Equal(0d, Guard.NotNegative(0d, "campo"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Guard.NotNegative(-0.5d, "campo"));
        }
    }

    public class DataSourceTests
    {
        [Fact]
        public void Crea_la_fuente_con_sus_datos()
        {
            var source = new DataSource(" osm ", "OpenStreetMap", DataSourceKind.Community, "OSM", "https://osm.org", "ODbL");

            Assert.Equal("osm", source.Code);
            Assert.Equal("OpenStreetMap", source.Name);
            Assert.Equal(DataSourceKind.Community, source.Kind);
            Assert.Equal("OSM", source.Publisher);
            Assert.Equal("https://osm.org", source.Url);
            Assert.Equal("ODbL", source.License);
            Assert.Equal(0, source.Id);
        }

        [Fact]
        public void Exige_codigo_y_nombre()
        {
            Assert.Throws<ArgumentException>(() => new DataSource("", "x", DataSourceKind.Official, null, null, null));
            Assert.Throws<ArgumentException>(() => new DataSource("x", " ", DataSourceKind.Official, null, null, null));
        }
    }

    public class TextNormalizerTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("   ", "")]
        [InlineData("Ramos Mejía", "RAMOS MEJIA")]
        [InlineData("  ramos   mejía ", "RAMOS MEJIA")]
        [InlineData("--Escuela N° 39 \"El Pampero\"!", "ESCUELA N 39 EL PAMPERO")]
        [InlineData("Ñuñoa", "NUNOA")]
        public void Normalize_quita_tildes_signos_y_espacios(string? input, string expected)
        {
            Assert.Equal(expected, TextNormalizer.Normalize(input));
        }

        [Fact]
        public void Slugify_une_las_partes_normalizadas_e_ignora_las_vacias()
        {
            Assert.Equal("ramos-mejia-la-matanza", TextNormalizer.Slugify("Ramos Mejía", null, "", "La Matanza"));
        }

        [Theory]
        [InlineData("LA MATANZA", "La Matanza")]
        [InlineData("LOMAS DE ZAMORA", "Lomas de Zamora")]
        [InlineData("COMUNA 10", "Comuna 10")]
        [InlineData("  VILLA  DEL PARQUE ", "Villa del Parque")]
        [InlineData("Islas de Zárate", "Islas de Zárate")]
        public void ToDisplayName_pasa_a_formato_titulo_solo_si_viene_en_mayusculas(string input, string expected)
        {
            Assert.Equal(expected, TextNormalizer.ToDisplayName(input));
        }
    }
}
