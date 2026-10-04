using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZonaMatch.Infrastructure.Geo
{
    // Punto unico de construccion de geometrias, con el SRID (WGS84) usado en toda la app
    public static class GeoFactory
    {
        public const int Srid = 4326;

        private static readonly GeometryFactory _factory =
            NtsGeometryServices.Instance.CreateGeometryFactory(Srid);

        // NetTopologySuite/PostGIS usan orden (X = longitud, Y = latitud)
        public static Point CrearPunto(double latitud, double longitud) =>
            _factory.CreatePoint(new Coordinate(longitud, latitud));
    }
}
