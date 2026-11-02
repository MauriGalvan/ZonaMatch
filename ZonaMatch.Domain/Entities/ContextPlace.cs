namespace ZonaMatch.Domain.Entities
{
    // A place selected in the wizard (app.puntos_interes table), not a geographic POI from the data dump.
    // This record belongs to a context and is deleted with it.
    public class ContextPlace
    {
        public Guid Id { get; set; }

        public Guid ContextId { get; set; }

        public Context? Context { get; set; }

        // Order in which the user added places to the wizard map.
        public int Order { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Address { get; set; }

        // Coordinates selected on the map; null if the place was not geocoded.
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? Type { get; set; }

        public string? WhoGoes { get; set; }

        public string? Frequency { get; set; }

        public string? Priority { get; set; }

        public string? MaximumTravelTime { get; set; }
    }
}
