using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ZonaMatch.Domain.Geo;

namespace ZonaMatch.Tests.Support
{
    // geometrías de prueba en WGS84 alrededor del AMBA
    public static class TestGeometry
    {
        private static readonly WKTReader Reader = new(new NetTopologySuite.NtsGeometryServices(
            GeometryNormalizer.Factory.PrecisionModel, SpatialReference.Wgs84));

        public static Geometry FromWkt(string wkt, int srid = SpatialReference.Wgs84)
        {
            var geometry = Reader.Read(wkt);
            geometry.SRID = srid;
            return geometry;
        }

        public static Polygon Square(double minLongitude, double minLatitude, double size)
        {
            var maxLongitude = minLongitude + size;
            var maxLatitude = minLatitude + size;
            return (Polygon)FromWkt(
                $"POLYGON(({Fmt(minLongitude)} {Fmt(minLatitude)}, {Fmt(maxLongitude)} {Fmt(minLatitude)}, " +
                $"{Fmt(maxLongitude)} {Fmt(maxLatitude)}, {Fmt(minLongitude)} {Fmt(maxLatitude)}, {Fmt(minLongitude)} {Fmt(minLatitude)}))");
        }

        public static MultiPolygon MultiSquare(double minLongitude, double minLatitude, double size) =>
            GeometryNormalizer.Factory.CreateMultiPolygon([Square(minLongitude, minLatitude, size)]);

        public static Point Point(double longitude, double latitude) =>
            GeometryNormalizer.Factory.CreatePoint(new Coordinate(longitude, latitude));

        private static string Fmt(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
