using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Geo
{
    // Google encoded polyline format, the one MOTIS uses for leg geometries
    // (https://developers.google.com/maps/documentation/utilities/polylinealgorithm), with a configurable precision.
    internal static class Polyline
    {
        public static IReadOnlyList<RoutePoint> Decode(string encoded, int precision)
        {
            var factor = Math.Pow(10, precision);
            var points = new List<RoutePoint>();
            int index = 0, latitude = 0, longitude = 0;

            while (index < encoded.Length)
            {
                latitude += NextValue(encoded, ref index);
                longitude += NextValue(encoded, ref index);
                points.Add(new RoutePoint(latitude / factor, longitude / factor));
            }

            return points;
        }

        // Each value is a zigzag-encoded delta in 5-bit chunks, offset by 63
        private static int NextValue(string encoded, ref int index)
        {
            int result = 0, shift = 0, chunk;
            do
            {
                if (index >= encoded.Length)
                    return 0;

                chunk = encoded[index++] - 63;
                result |= (chunk & 0x1F) << shift;
                shift += 5;
            }
            while (chunk >= 0x20);

            return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
        }
    }
}
