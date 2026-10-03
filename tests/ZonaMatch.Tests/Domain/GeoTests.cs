using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Geo;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Domain
{
    public class GeometryNormalizerTests
    {
        [Fact]
        public void ToWgs84_rechaza_null()
        {
            Assert.Throws<ArgumentNullException>(() => GeometryNormalizer.ToWgs84(null!));
        }

        [Fact]
        public void ToWgs84_copia_las_geometrias_que_ya_estan_en_4326()
        {
            var original = TestGeometry.Point(-58.5, -34.6);

            var result = GeometryNormalizer.ToWgs84(original);

            Assert.NotSame(original, result);
            Assert.Equal(SpatialReference.Wgs84, result.SRID);
            Assert.True(result.EqualsExact(original));
        }

        [Fact]
        public void ToWgs84_transforma_web_mercator()
        {
            // Obelisco en EPSG:3857
            var mercator = TestGeometry.FromWkt("POLYGON((-6498200 -4110700, -6498100 -4110700, -6498100 -4110600, -6498200 -4110700))", SpatialReference.WebMercator);

            var result = GeometryNormalizer.ToWgs84(mercator);

            Assert.Equal(SpatialReference.Wgs84, result.SRID);
            Assert.InRange(result.Coordinates[0].X, -58.38, -58.37);
            Assert.InRange(result.Coordinates[0].Y, -34.61, -34.60);
        }

        [Fact]
        public void ToWgs84_rechaza_proyecciones_desconocidas()
        {
            var geometry = TestGeometry.FromWkt("POINT(1 1)", 22185);

            Assert.Throws<NotSupportedException>(() => GeometryNormalizer.ToWgs84(geometry));
        }

        [Fact]
        public void ToMultiPolygon_envuelve_un_poligono()
        {
            var result = GeometryNormalizer.ToMultiPolygon(TestGeometry.Square(-58.5, -34.6, 0.01));

            Assert.Equal(1, result.NumGeometries);
            Assert.Equal(SpatialReference.Wgs84, result.SRID);
        }

        [Fact]
        public void ToMultiPolygon_aplana_colecciones_anidadas_y_descarta_vacios()
        {
            var collection = TestGeometry.FromWkt(
                "GEOMETRYCOLLECTION(MULTIPOLYGON(((0 0, 1 0, 1 1, 0 0))), POLYGON EMPTY, MULTIPOLYGON(((5 5, 6 5, 6 6, 5 5))))");

            var result = GeometryNormalizer.ToMultiPolygon(collection);

            Assert.Equal(2, result.NumGeometries);
        }

        [Fact]
        public void ToMultiPolygon_repara_geometrias_invalidas()
        {
            // moño: el anillo se cruza a sí mismo
            var bowtie = TestGeometry.FromWkt("POLYGON((0 0, 2 2, 2 0, 0 2, 0 0))");

            var result = GeometryNormalizer.ToMultiPolygon(bowtie);

            Assert.True(result.IsValid);
            Assert.Equal(2, result.NumGeometries);
        }

        [Fact]
        public void ToMultiPolygon_disuelve_partes_superpuestas()
        {
            var overlapping = TestGeometry.FromWkt("GEOMETRYCOLLECTION(POLYGON((0 0, 2 0, 2 2, 0 2, 0 0)), POLYGON((1 1, 3 1, 3 3, 1 3, 1 1)))");

            var result = GeometryNormalizer.ToMultiPolygon(overlapping);

            Assert.True(result.IsValid);
            Assert.Equal(1, result.NumGeometries);
            Assert.Equal(7d, result.Area, 6);
        }

        [Fact]
        public void ToMultiPolygon_rechaza_geometrias_sin_superficie()
        {
            var line = TestGeometry.FromWkt("LINESTRING(0 0, 1 1)");

            Assert.Throws<ArgumentException>(() => GeometryNormalizer.ToMultiPolygon(line));
        }

        [Fact]
        public void ToRepresentativePoint_conserva_los_puntos()
        {
            var result = GeometryNormalizer.ToRepresentativePoint(TestGeometry.Point(-58.5, -34.6));

            Assert.Equal(-58.5, result.X);
            Assert.Equal(-34.6, result.Y);
        }

        [Fact]
        public void ToRepresentativePoint_usa_un_punto_interior_de_los_poligonos()
        {
            var square = TestGeometry.Square(-58.5, -34.6, 0.01);

            var result = GeometryNormalizer.ToRepresentativePoint(square);

            Assert.True(square.Contains(result));
            Assert.Equal(SpatialReference.Wgs84, result.SRID);
        }

        [Fact]
        public void ToRepresentativePoint_rechaza_geometrias_vacias()
        {
            Assert.Throws<ArgumentException>(() => GeometryNormalizer.ToRepresentativePoint(TestGeometry.FromWkt("POINT EMPTY")));
        }
    }

    public class GeoMathTests
    {
        [Fact]
        public void DistanceMeters_calcula_la_distancia_geodesica()
        {
            // un grado de latitud ~ 111,2 km
            Assert.InRange(GeoMath.DistanceMeters(0, 0, 1, 0), 111_150, 111_250);
            Assert.Equal(0d, GeoMath.DistanceMeters(TestGeometry.Point(-58.5, -34.6), TestGeometry.Point(-58.5, -34.6)));
        }

        [Fact]
        public void AreaSquareMeters_resta_los_huecos()
        {
            var square = (MultiPolygon)TestGeometry.FromWkt("MULTIPOLYGON(((0 0, 0.01 0, 0.01 0.01, 0 0.01, 0 0)))");
            var withHole = (MultiPolygon)TestGeometry.FromWkt(
                "MULTIPOLYGON(((0 0, 0.01 0, 0.01 0.01, 0 0.01, 0 0), (0.002 0.002, 0.004 0.002, 0.004 0.004, 0.002 0.004, 0.002 0.002)))");

            var full = GeoMath.AreaSquareMeters(square);
            var holed = GeoMath.AreaSquareMeters(withHole);

            Assert.InRange(full, 1_238_500, 1_240_000);
            Assert.InRange(full - holed, 49_000, 50_000);
        }
    }
}
