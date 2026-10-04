namespace ZonaMatch.Infrastructure.Geo
{
    // Distance helpers for "geometry" columns in SRID 4326, whose native unit is degrees (not meters)
    public static class GeoMath
    {
        private const double EarthRadiusMeters = 6_371_000d;
        private const double MetersPerLatitudeDegree = 111_320d;

        // Great-circle distance in meters (haversine)
        public static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Pow(Math.Sin(dLat / 2), 2)
                    + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);

            return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
        }

        // Upper bound, in degrees, of a radius in meters: uses the longitude degree, the shortest one at this latitude,
        // so a degree-based ST_DWithin never discards a point that is really inside the radius.
        public static double MetrosAGrados(double metros, double latitud) =>
            metros / (MetersPerLatitudeDegree * Math.Cos(ToRadians(latitud)));

        private static double ToRadians(double grados) => grados * Math.PI / 180d;
    }
}
