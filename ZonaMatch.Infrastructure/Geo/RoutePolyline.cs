namespace ZonaMatch.Infrastructure.Geo;

// Decodes a MOTIS polyline and sums its segments using the declared coordinate precision.
public static class RoutePolyline
{
    public static double? GetLengthInMeters(string? encodedPolyline, int? precision)
    {
        if (string.IsNullOrEmpty(encodedPolyline)
            || precision is not { } decimalPlaces
            || decimalPlaces is < 0 or > 7)
            return null;

        var factor = Math.Pow(10, decimalPlaces);
        var index = 0;
        var pointCount = 0;
        var latitude = 0.0;
        var longitude = 0.0;
        var distanceMeters = 0.0;
        (double Latitude, double Longitude)? previous = null;

        while (index < encodedPolyline.Length)
        {
            if (!ReadDelta(encodedPolyline, ref index, out var latitudeDelta)
                || !ReadDelta(encodedPolyline, ref index, out var longitudeDelta))
                return null;

            latitude += latitudeDelta / factor;
            longitude += longitudeDelta / factor;

            if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
                return null;

            if (previous is { } previousPoint)
                distanceMeters += GeoMath.CalculateDistanceInMeters(
                    previousPoint.Latitude, previousPoint.Longitude, latitude, longitude);

            previous = (latitude, longitude);
            pointCount++;
        }

        return pointCount >= 2 ? distanceMeters : null;
    }

    private static bool ReadDelta(string encodedPolyline, ref int index, out long delta)
    {
        delta = 0;
        long value = 0;
        var shift = 0;

        while (index < encodedPolyline.Length && shift <= 55)
        {
            var chunk = encodedPolyline[index++] - 63;
            if (chunk is < 0 or > 63)
                return false;

            value |= (long)(chunk & 31) << shift;
            if (chunk < 32)
            {
                delta = (value & 1) != 0 ? ~(value >> 1) : value >> 1;
                return true;
            }

            shift += 5;
        }

        return false;
    }
}
