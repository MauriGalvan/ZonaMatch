using ZonaMatch.Domain.Territory;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Domain
{
    public class TerritorialUnitTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        private static TerritorialUnit Partido(string name = "LA MATANZA", string? code = "427") =>
            TerritorialUnit.Create(TerritorialUnitType.Partido, code, name, TestGeometry.Square(-58.6, -34.7, 0.05), 2, "427", new DateOnly(2022, 5, 18), Now);

        [Fact]
        public void Create_normaliza_nombre_geometria_y_area()
        {
            var unit = Partido();

            Assert.Equal(TerritorialUnitType.Partido, unit.Type);
            Assert.Equal("427", unit.Code);
            Assert.Equal("La Matanza", unit.Name);
            Assert.Equal("LA MATANZA", unit.NormalizedName);
            Assert.Equal(1, unit.Geometry.NumGeometries);
            Assert.True(unit.AreaM2 > 0);
            Assert.Equal((short)2, unit.DataSourceId);
            Assert.Equal("427", unit.ExternalId);
            Assert.Equal(new DateOnly(2022, 5, 18), unit.SourceDate);
            Assert.Equal(Now, unit.ImportedAt);
            Assert.Null(unit.ParentId);
            Assert.Null(unit.Parent);
            Assert.Equal(0, unit.Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public void Create_guarda_codigo_null_si_viene_vacio(string? code)
        {
            Assert.Null(Partido(code: code).Code);
        }

        [Fact]
        public void Create_exige_nombre()
        {
            Assert.Throws<ArgumentException>(() => Partido(name: " "));
        }

        [Fact]
        public void Constructor_valida_invariantes()
        {
            var geometry = TestGeometry.MultiSquare(0, 0, 1);

            Assert.Throws<ArgumentException>(() => new TerritorialUnit(TerritorialUnitType.Barrio, null, "x", "", geometry, 1, 1, "1", null, Now));
            Assert.Throws<ArgumentNullException>(() => new TerritorialUnit(TerritorialUnitType.Barrio, null, "x", "X", null!, 1, 1, "1", null, Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerritorialUnit(TerritorialUnitType.Barrio, null, "x", "X", geometry, -1, 1, "1", null, Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerritorialUnit(TerritorialUnitType.Barrio, null, "x", "X", geometry, 1, 0, "1", null, Now));
            Assert.Throws<ArgumentException>(() => new TerritorialUnit(TerritorialUnitType.Barrio, null, "x", "X", geometry, 1, 1, "", null, Now));
        }

        [Fact]
        public void AssignParent_exige_id_positivo()
        {
            var unit = Partido();

            Assert.Throws<ArgumentOutOfRangeException>(() => unit.AssignParent(0));
            unit.AssignParent(7);
            Assert.Equal(7, unit.ParentId);
        }

        [Fact]
        public void UpdateFrom_copia_todo_menos_la_identidad()
        {
            var current = Partido();
            var incoming = TerritorialUnit.Create(TerritorialUnitType.Localidad, "X1", "RAMOS MEJIA", TestGeometry.Square(-58.57, -34.66, 0.02), 5, "otro", null, Now.AddDays(1));
            incoming.AssignParent(3);

            current.UpdateFrom(incoming);

            Assert.Equal(TerritorialUnitType.Localidad, current.Type);
            Assert.Equal("X1", current.Code);
            Assert.Equal("Ramos Mejia", current.Name);
            Assert.Equal("RAMOS MEJIA", current.NormalizedName);
            Assert.Same(incoming.Geometry, current.Geometry);
            Assert.Equal(incoming.AreaM2, current.AreaM2);
            Assert.Null(current.SourceDate);
            Assert.Equal(Now.AddDays(1), current.ImportedAt);
            Assert.Equal(3, current.ParentId);
            Assert.Equal("427", current.ExternalId);
            Assert.Equal((short)2, current.DataSourceId);
            Assert.Throws<ArgumentNullException>(() => current.UpdateFrom(null!));
        }
    }

    public class ZoneTests
    {
        [Fact]
        public void Constructor_valida_invariantes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Zone(0, "s", "n", "p"));
            Assert.Throws<ArgumentException>(() => new Zone(1, "", "n", "p"));
            Assert.Throws<ArgumentException>(() => new Zone(1, "s", "", "p"));
            Assert.Throws<ArgumentException>(() => new Zone(1, "s", "n", ""));
        }

        [Theory]
        [InlineData(TerritorialUnitType.Barrio, true)]
        [InlineData(TerritorialUnitType.Localidad, true)]
        [InlineData(TerritorialUnitType.Comuna, false)]
        [InlineData(TerritorialUnitType.Partido, false)]
        [InlineData(TerritorialUnitType.CensusTract, false)]
        public void IsAnalyzable_solo_barrios_y_localidades(TerritorialUnitType type, bool expected)
        {
            Assert.Equal(expected, Zone.IsAnalyzable(type));
        }

        [Fact]
        public void Create_barrio_cuelga_de_CABA()
        {
            var zone = Zone.Create(10, TerritorialUnitType.Barrio, "Villa Luro", null);

            Assert.Equal("villa-luro-caba", zone.Slug);
            Assert.Equal("Villa Luro", zone.Name);
            Assert.Equal(Zone.CabaName, zone.ParentName);
            Assert.Equal("Villa Luro, CABA", zone.DisplayName);
            Assert.Equal(10, zone.TerritorialUnitId);
            Assert.Null(zone.TerritorialUnit);
            Assert.Equal(0, zone.Id);
        }

        [Fact]
        public void Create_localidad_cuelga_de_su_partido()
        {
            var zone = Zone.Create(11, TerritorialUnitType.Localidad, "Ramos Mejía", "La Matanza");

            Assert.Equal("ramos-mejia-la-matanza", zone.Slug);
            Assert.Equal("Ramos Mejía, La Matanza", zone.DisplayName);
        }

        [Fact]
        public void Create_rechaza_localidad_sin_partido_y_tipos_no_analizables()
        {
            Assert.Throws<ArgumentException>(() => Zone.Create(11, TerritorialUnitType.Localidad, "Haedo", null));
            Assert.Throws<ArgumentException>(() => Zone.Create(11, TerritorialUnitType.Partido, "Morón", "Morón"));
        }

        [Fact]
        public void DisambiguateSlug_agrega_sufijo()
        {
            var zone = Zone.Create(11, TerritorialUnitType.Localidad, "San Justo", "La Matanza");

            Assert.Throws<ArgumentOutOfRangeException>(() => zone.DisambiguateSlug(0));
            zone.DisambiguateSlug(2);
            Assert.Equal("san-justo-la-matanza-2", zone.Slug);
        }

        [Fact]
        public void UpdateFrom_copia_slug_y_nombres()
        {
            var zone = Zone.Create(11, TerritorialUnitType.Localidad, "San Justo", "La Matanza");
            var incoming = Zone.Create(99, TerritorialUnitType.Barrio, "Liniers", null);

            zone.UpdateFrom(incoming);

            Assert.Equal("liniers-caba", zone.Slug);
            Assert.Equal("Liniers", zone.Name);
            Assert.Equal(Zone.CabaName, zone.ParentName);
            Assert.Equal(11, zone.TerritorialUnitId);
            Assert.Throws<ArgumentNullException>(() => zone.UpdateFrom(null!));
        }

        [Fact]
        public void ZoneAdjacency_valida_ids()
        {
            Assert.Throws<ArgumentException>(() => new ZoneAdjacency(3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneAdjacency(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneAdjacency(3, -1));

            var adjacency = new ZoneAdjacency(3, 4);
            Assert.Equal(3, adjacency.ZoneId);
            Assert.Equal(4, adjacency.NeighborZoneId);
        }
    }

    public class TerritorialIndicatorTests
    {
        [Fact]
        public void Crea_el_indicador_con_su_procedencia()
        {
            var indicator = new TerritorialIndicator(5, IndicatorCodes.Population, 34000m, "hab", 3, new DateOnly(2022, 5, 18));

            Assert.Equal(5, indicator.TerritorialUnitId);
            Assert.Equal("population", indicator.IndicatorCode);
            Assert.Equal(34000m, indicator.Value);
            Assert.Equal("hab", indicator.Unit);
            Assert.Equal((short)3, indicator.DataSourceId);
            Assert.Equal(new DateOnly(2022, 5, 18), indicator.ReferenceDate);
            Assert.Equal(0, indicator.Id);
        }

        [Fact]
        public void Valida_invariantes()
        {
            var date = new DateOnly(2025, 1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerritorialIndicator(0, "x", 1, "u", 1, date));
            Assert.Throws<ArgumentException>(() => new TerritorialIndicator(1, "", 1, "u", 1, date));
            Assert.Throws<ArgumentException>(() => new TerritorialIndicator(1, "x", 1, "", 1, date));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerritorialIndicator(1, "x", 1, "u", 0, date));
        }
    }
}
