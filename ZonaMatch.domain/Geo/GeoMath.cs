using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Geo
{
    // cálculos geodésicos sobre WGS84 sin depender de la base
    public static class GeoMath
    {
        // radio medio de la Tierra (IUGG)
        private const double MeanEarthRadiusMeters = 6371008.8d;
        // radio usado para áreas esféricas (mismo criterio que Turf / PostGIS geography aproximado)
        private const double AuthalicRadiusMeters = 6378137d;

        public static double DistanceMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
        {
            var deltaLatitude = ToRadians(latitudeB - latitudeA);
            var deltaLongitude = ToRadians(longitudeB - longitudeA);

            var a = Math.Pow(Math.Sin(deltaLatitude / 2d), 2d)
                + Math.Cos(ToRadians(latitudeA)) * Math.Cos(ToRadians(latitudeB)) * Math.Pow(Math.Sin(deltaLongitude / 2d), 2d);

            return 2d * MeanEarthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
        }

        public static double DistanceMeters(Point a, Point b)
        {
            return DistanceMeters(a.Y, a.X, b.Y, b.X);
        }

        public static double AreaSquareMeters(MultiPolygon multiPolygon)
        {
            var total = 0d;

            foreach (var polygon in multiPolygon.Geometries.Cast<Polygon>())
            {
                total += Math.Abs(RingArea(polygon.ExteriorRing.Coordinates));

                foreach (var hole in polygon.InteriorRings)
                {
                    total -= Math.Abs(RingArea(hole.Coordinates));
                }
            }

            return total;
        }

        private static double RingArea(Coordinate[] coordinates)
        {
            var area = 0d;
            var count = coordinates.Length;

            for (var index = 0; index < count - 1; index++)
            {
                var lower = coordinates[index];
                var upper = coordinates[index + 1];
                area += ToRadians(upper.X - lower.X) * (2d + Math.Sin(ToRadians(lower.Y)) + Math.Sin(ToRadians(upper.Y)));
            }

            return area * AuthalicRadiusMeters * AuthalicRadiusMeters / 2d;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
    }
}
