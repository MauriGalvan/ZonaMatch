using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Domain
{
    public class PointOfInterestTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        private static PointOfInterest Create(NetTopologySuite.Geometries.Geometry geometry, string? name = "Escuela N° 39") =>
            PointOfInterest.Create(202, name, "Escuela primaria", " Av. de Mayo 700 ", geometry, Ownership.Public,
                new Dictionary<string, string> { ["nivel"] = "Nivel Primario" }, 4, "e-39", new DateOnly(2026, 3, 1), Now);

        [Fact]
        public void Create_desde_un_punto_no_guarda_huella()
        {
            var poi = Create(TestGeometry.Point(-58.56, -34.65));

            Assert.Equal((short)202, poi.CategoryId);
            Assert.Equal("Escuela N° 39", poi.Name);
            Assert.Equal("ESCUELA N 39", poi.NormalizedName);
            Assert.Equal("Av. de Mayo 700", poi.Address);
            Assert.Equal(-58.56, poi.Location.X);
            Assert.Null(poi.Footprint);
            Assert.Equal(Ownership.Public, poi.Ownership);
            Assert.Equal("Nivel Primario", poi.Attributes["nivel"]);
            Assert.Equal((short)4, poi.DataSourceId);
            Assert.Equal("e-39", poi.ExternalId);
            Assert.Equal(new DateOnly(2026, 3, 1), poi.SourceDate);
            Assert.Equal(Now, poi.ImportedAt);
            Assert.Null(poi.CanonicalPoiId);
            Assert.Null(poi.Category);
            Assert.Equal(0, poi.Id);
        }

        [Fact]
        public void Create_desde_un_poligono_guarda_la_huella_y_un_punto_interior()
        {
            var square = TestGeometry.Square(-58.56, -34.65, 0.002);

            var poi = Create(square);

            Assert.NotNull(poi.Footprint);
            Assert.True(square.Contains(poi.Location));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public void Create_usa_el_nombre_de_la_categoria_si_la_fuente_no_trae_nombre(string? name)
        {
            Assert.Equal("Escuela primaria", Create(TestGeometry.Point(0, 0), name).Name);
        }

        [Fact]
        public void Create_valida_geometria_y_nombre_alternativo()
        {
            Assert.Throws<ArgumentNullException>(() => Create(null!));
            Assert.Throws<ArgumentException>(() => PointOfInterest.Create(202, null, " ", null, TestGeometry.Point(0, 0),
                Ownership.Unknown, [], 4, "x", null, Now));
        }

        [Fact]
        public void Constructor_valida_invariantes_y_direccion_vacia()
        {
            var point = TestGeometry.Point(0, 0);

            Assert.Null(new PointOfInterest(1, "n", "N", "  ", point, null, Ownership.Unknown, [], 1, "x", null, Now).Address);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PointOfInterest(0, "n", "N", null, point, null, Ownership.Unknown, [], 1, "x", null, Now));
            Assert.Throws<ArgumentException>(() => new PointOfInterest(1, "", "N", null, point, null, Ownership.Unknown, [], 1, "x", null, Now));
            Assert.Throws<ArgumentException>(() => new PointOfInterest(1, "n", "", null, point, null, Ownership.Unknown, [], 1, "x", null, Now));
            Assert.Throws<ArgumentNullException>(() => new PointOfInterest(1, "n", "N", null, null!, null, Ownership.Unknown, [], 1, "x", null, Now));
            Assert.Throws<ArgumentNullException>(() => new PointOfInterest(1, "n", "N", null, point, null, Ownership.Unknown, null!, 1, "x", null, Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PointOfInterest(1, "n", "N", null, point, null, Ownership.Unknown, [], 0, "x", null, Now));
            Assert.Throws<ArgumentException>(() => new PointOfInterest(1, "n", "N", null, point, null, Ownership.Unknown, [], 1, "", null, Now));
        }

        [Fact]
        public void MarkAsDuplicateOf_vincula_al_canonico()
        {
            var poi = Create(TestGeometry.Point(0, 0));

            Assert.Throws<ArgumentException>(() => poi.MarkAsDuplicateOf(poi.Id));
            Assert.Throws<ArgumentOutOfRangeException>(() => poi.MarkAsDuplicateOf(-4));

            poi.MarkAsDuplicateOf(15);
            Assert.Equal(15, poi.CanonicalPoiId);
        }

        [Fact]
        public void UpdateFrom_copia_los_datos_de_la_fuente()
        {
            var current = Create(TestGeometry.Point(0, 0));
            var incoming = PointOfInterest.Create(203, "Secundaria 2", "x", null, TestGeometry.Square(1, 1, 0.001), Ownership.Private,
                new Dictionary<string, string> { ["nivel"] = "Nivel Secundario" }, 9, "otro", null, Now.AddDays(2));

            current.UpdateFrom(incoming);

            Assert.Equal((short)203, current.CategoryId);
            Assert.Equal("Secundaria 2", current.Name);
            Assert.Equal("SECUNDARIA 2", current.NormalizedName);
            Assert.Null(current.Address);
            Assert.Same(incoming.Location, current.Location);
            Assert.Same(incoming.Footprint, current.Footprint);
            Assert.Equal(Ownership.Private, current.Ownership);
            Assert.Same(incoming.Attributes, current.Attributes);
            Assert.Null(current.SourceDate);
            Assert.Equal(Now.AddDays(2), current.ImportedAt);
            Assert.Equal("e-39", current.ExternalId);
            Assert.Throws<ArgumentNullException>(() => current.UpdateFrom(null!));
        }
    }

    public class PoiCatalogTests
    {
        [Fact]
        public void PoiCategory_distingue_raices_de_subcategorias()
        {
            var root = new PoiCategory("salud", "Salud", null, 3);
            var child = new PoiCategory("salud.hospital", "Hospital", 3, 1);

            Assert.True(root.IsRoot);
            Assert.False(child.IsRoot);
            Assert.Equal("salud.hospital", child.Code);
            Assert.Equal("Hospital", child.Name);
            Assert.Equal((short)3, child.ParentId);
            Assert.Equal((short)1, child.SortOrder);
            Assert.Equal(0, child.Id);
            Assert.Throws<ArgumentException>(() => new PoiCategory("", "x", null, 1));
            Assert.Throws<ArgumentException>(() => new PoiCategory("x", "", null, 1));
        }

        [Fact]
        public void PoiMappingRule_valida_invariantes()
        {
            var rule = new PoiMappingRule(5, "amenity", "school", 204, 10);

            Assert.Equal((short)5, rule.DataSourceId);
            Assert.Equal("amenity", rule.Attribute);
            Assert.Equal("school", rule.Value);
            Assert.Equal((short)204, rule.CategoryId);
            Assert.Equal(10, rule.Priority);
            Assert.Equal(0, rule.Id);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoiMappingRule(0, "a", "v", 1, 1));
            Assert.Throws<ArgumentException>(() => new PoiMappingRule(1, "", "v", 1, 1));
            Assert.Throws<ArgumentException>(() => new PoiMappingRule(1, "a", "", 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoiMappingRule(1, "a", "v", 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoiMappingRule(1, "a", "v", 1, -1));
        }
    }

    public class PoiClassifierTests
    {
        private readonly PoiClassifier _classifier = new(
        [
            new PoiMappingRule(5, "amenity", "school", 204, 10),
            new PoiMappingRule(5, "amenity", "clinic", 302, 10),
            new PoiMappingRule(5, "healthcare", "centre", 303, 5),
            new PoiMappingRule(4, "nivel", "Nivel Primario", 202, 10),
        ]);

        [Fact]
        public void Valida_argumentos()
        {
            Assert.Throws<ArgumentNullException>(() => new PoiClassifier(null!));
            Assert.Throws<ArgumentNullException>(() => _classifier.Classify(5, null!));
        }

        [Fact]
        public void Clasifica_ignorando_mayusculas_y_espacios()
        {
            Assert.Equal((short)204, _classifier.Classify(5, new Dictionary<string, string> { ["amenity"] = " School " }));
            Assert.Equal((short)202, _classifier.Classify(4, new Dictionary<string, string> { ["nivel"] = "nivel primario" }));
        }

        [Fact]
        public void Gana_la_regla_de_menor_prioridad()
        {
            var attributes = new Dictionary<string, string> { ["amenity"] = "clinic", ["healthcare"] = "centre" };

            Assert.Equal((short)303, _classifier.Classify(5, attributes));
        }

        [Fact]
        public void Devuelve_null_si_ninguna_regla_aplica()
        {
            Assert.Null(_classifier.Classify(5, new Dictionary<string, string> { ["amenity"] = "parking" }));
            Assert.Null(_classifier.Classify(5, new Dictionary<string, string> { ["shop"] = "bakery" }));
            Assert.Null(_classifier.Classify(99, new Dictionary<string, string> { ["amenity"] = "school" }));
        }
    }

    public class PoiDuplicateMatcherTests
    {
        private static readonly PoiDuplicateMatcher Matcher = new(100);

        private static PoiMatchCandidate Candidate(long id, string name, double longitude, DataSourceKind kind) =>
            new(id, name, TestGeometry.Point(longitude, -34.65), kind);

        [Fact]
        public void Valida_argumentos()
        {
            var candidate = Candidate(1, "A", -58.5, DataSourceKind.Official);

            Assert.Throws<ArgumentOutOfRangeException>(() => new PoiDuplicateMatcher(0));
            Assert.Throws<ArgumentNullException>(() => Matcher.Match(null!, candidate));
            Assert.Throws<ArgumentNullException>(() => Matcher.Match(candidate, null!));
        }

        [Fact]
        public void Mismo_numero_cerca_es_el_mismo_lugar_y_gana_la_fuente_oficial()
        {
            var osm = Candidate(10, "ESCUELA N 039", -58.5000, DataSourceKind.Community);
            var padron = Candidate(20, "ESCUELA DE EDUCACION PRIMARIA N 39 EL PAMPERO", -58.5004, DataSourceKind.Official);

            Assert.Equal(new PoiDuplicate(10, 20), Matcher.Match(osm, padron));
            Assert.Equal(new PoiDuplicate(10, 20), Matcher.Match(padron, osm));
        }

        [Fact]
        public void Numeros_distintos_no_son_el_mismo_lugar()
        {
            var first = Candidate(1, "ESCUELA N 39", -58.5000, DataSourceKind.Community);
            var second = Candidate(2, "ESCUELA N 40", -58.5001, DataSourceKind.Official);

            Assert.Null(Matcher.Match(first, second));
        }

        [Fact]
        public void Lejos_no_son_el_mismo_lugar()
        {
            var first = Candidate(1, "HOSPITAL POSADAS", -58.50, DataSourceKind.Community);
            var second = Candidate(2, "HOSPITAL POSADAS", -58.51, DataSourceKind.Official);

            Assert.Null(Matcher.Match(first, second));
        }

        [Fact]
        public void Sin_numeros_compara_palabras_distintivas()
        {
            var first = Candidate(1, "COLEGIO SAN JOSE", -58.5000, DataSourceKind.Community);
            var second = Candidate(2, "INSTITUTO SAN JOSE OBRERO", -58.5005, DataSourceKind.Community);
            var other = Candidate(3, "JARDIN LOS PITUFOS", -58.5001, DataSourceKind.Community);
            var numbered = Candidate(4, "SAN JOSE 2", -58.5002, DataSourceKind.Community);

            // mismo tipo de fuente: gana el id más bajo, en cualquier orden
            Assert.Equal(new PoiDuplicate(2, 1), Matcher.Match(first, second));
            Assert.Equal(new PoiDuplicate(2, 1), Matcher.Match(second, first));
            Assert.Null(Matcher.Match(first, other));
            // si solo uno tiene número, se comparan las palabras
            Assert.Equal(new PoiDuplicate(4, 1), Matcher.Match(first, numbered));
        }

        [Fact]
        public void Nombres_genericos_solo_coinciden_muy_cerca()
        {
            var first = Candidate(1, "ESCUELA PRIMARIA", -58.5000, DataSourceKind.Community);
            var nearby = Candidate(2, "ESCUELA N 0", -58.5002, DataSourceKind.Official);
            var further = Candidate(3, "ESCUELA", -58.5008, DataSourceKind.Official);

            Assert.Equal(new PoiDuplicate(1, 2), Matcher.Match(first, nearby));
            Assert.Null(Matcher.Match(first, further));
        }
    }
}
