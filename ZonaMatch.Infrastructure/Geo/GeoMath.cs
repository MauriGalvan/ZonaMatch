namespace ZonaMatch.Infrastructure.Geo;

// Distance helpers for geometry columns in SRID 4326, whose native unit is degrees rather than meters.
public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000d;
    private const double MetersPerLatitudeDegree = 111_320d;

    // Great-circle distance in meters, calculated with the haversine formula.
    public static double CalculateDistanceInMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var deltaLatitude = ToRadians(latitude2 - latitude1);
        var deltaLongitude = ToRadians(longitude2 - longitude1);

        var a = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
                + Math.Cos(ToRadians(latitude1)) * Math.Cos(ToRadians(latitude2)) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }

    // Upper bound in degrees for a radius in meters, using the longitude degree at the given latitude.
    // This ensures degree-based ST_DWithin does not discard a point that is inside the radius.
    public static double ConvertMetersToDegrees(double meters, double latitude) =>
        meters / (MetersPerLatitudeDegree * Math.Cos(ToRadians(latitude)));

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
