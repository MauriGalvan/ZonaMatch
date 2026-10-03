using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Geo;

namespace ZonaMatch.Domain.Scoring
{
    public enum TravelMode
    {
        Walk,
        Bike,
        Car,
        PublicTransport,
    }

    // estimación por distancia mientras no esté OSRM (PBI 9) ni el ruteo de transporte público (PBI 70)
    public static class TravelTimeEstimator
    {
        // las calles no van en línea recta
        public const double DetourFactor = 1.3d;

        public static double SpeedKmh(TravelMode mode) => mode switch
        {
            TravelMode.Walk => 4.5d,
            TravelMode.Bike => 14d,
            TravelMode.Car => 25d,
            TravelMode.PublicTransport => 18d,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Modo de viaje desconocido."),
        };

        public static double EstimateMinutes(Point from, Point to, TravelMode mode)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);

            var kilometers = GeoMath.DistanceMeters(from, to) / 1000d * DetourFactor;
            return Math.Round(kilometers / SpeedKmh(mode) * 60d, 1);
        }

        // tiempo semanal (PBI 33a): ida y vuelta por cada viaje
        public static double WeeklyMinutes(IEnumerable<(double Minutes, int TripsPerWeek)> trips)
        {
            ArgumentNullException.ThrowIfNull(trips);

            return Math.Round(trips.Sum(trip => trip.Minutes * 2d * trip.TripsPerWeek), 1);
        }
    }
}
