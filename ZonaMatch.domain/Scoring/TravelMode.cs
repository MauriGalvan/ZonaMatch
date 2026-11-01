namespace ZonaMatch.Domain.Scoring
{
    public enum TravelMode
    {
        PublicTransit,
        Car,
        Bicycle,
        Walking,
    }

    public static class TravelModes
    {
        // Derive the travel mode from the household's mobility options (walking, public transit, car, or bicycle).
        // If no mode is provided, or public transit is selected, use public transit because most residents use it.
        public static TravelMode FromMobility(IReadOnlyCollection<string> mobility)
        {
            if (mobility.Count == 0 || mobility.Contains("transporte-publico"))
                return TravelMode.PublicTransit;

            if (mobility.Contains("vehiculo"))
                return TravelMode.Car;

            if (mobility.Contains("bici"))
                return TravelMode.Bicycle;

            return TravelMode.Walking;
        }

        // In public transit, five blocks or less are walked: taking a bus for three blocks is impractical. A household
        // that travels by car or bicycle keeps its mode. Use straight-line distance, as in Estimate.
        public const double WalkingDistanceThresholdKm = 0.5;

        public static TravelMode ForDistance(
            TravelMode mode,
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude) =>
            mode == TravelMode.PublicTransit
            && DistanceKm(originLatitude, originLongitude, destinationLatitude, destinationLongitude) <= WalkingDistanceThresholdKm
                ? TravelMode.Walking
                : mode;

        // Inverse of ToCode; no code means public transit, as in FromMobility. Null for an unknown code.
        public static TravelMode? FromCode(string? code) => code?.Trim().ToLowerInvariant() switch
        {
            null or "" or "transporte-publico" => TravelMode.PublicTransit,
            "auto" => TravelMode.Car,
            "bici" => TravelMode.Bicycle,
            "a-pie" => TravelMode.Walking,
            _ => null,
        };

        public static string ToCode(TravelMode mode) => mode switch
        {
            TravelMode.Car => "auto",
            TravelMode.Bicycle => "bici",
            TravelMode.Walking => "a-pie",
            _ => "transporte-publico",
        };

        // Estimate travel time from straight-line distance when the routing service does not respond.
        // Uses average AMBA speeds and a fixed wait for transit or parking.
        public static TravelTime Estimate(
            TravelMode mode,
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude)
        {
            var distanceKm = DistanceKm(originLatitude, originLongitude, destinationLatitude, destinationLongitude);
            var minutes = mode switch
            {
                TravelMode.Car => 5 + distanceKm * 2.5,
                TravelMode.Bicycle => distanceKm * 4,
                TravelMode.Walking => distanceKm * 12,
                _ => 10 + distanceKm * 4,
            };

            return new TravelTime((int)Math.Round(minutes), Estimado: true);
        }

        // Haversine distance in kilometers.
        public static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            const double EarthRadiusKm = 6371;
            var latitudeDelta = ToRadians(latitude2 - latitude1);
            var longitudeDelta = ToRadians(longitude2 - longitude1);
            var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
                            + Math.Cos(ToRadians(latitude1)) * Math.Cos(ToRadians(latitude2)) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

            return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(haversine));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    }
}
