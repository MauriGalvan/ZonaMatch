using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZonaMatch.Infrastructure.Geo
{
    // Creates geometries with the SRID (WGS84) used throughout the application.
    public static class GeoFactory
    {
        public const int Srid = 4326;

        private static readonly GeometryFactory _factory =
            NtsGeometryServices.Instance.CreateGeometryFactory(Srid);

        // NetTopologySuite and PostGIS use (X = longitude, Y = latitude) coordinate order.
        public static Point CreatePoint(double latitude, double longitude) =>
            _factory.CreatePoint(new Coordinate(longitude, latitude));
    }
}
