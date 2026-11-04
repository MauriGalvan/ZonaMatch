namespace ZonaMatch.Domain.Entities
{
    // A zone compared in an analysis (app.analisis_zonas table). Zones are referenced by slug,
    // as they are in community features; they are not stored in an application table.
    public class AnalysisZone
    {
        public Guid AnalysisId { get; set; }

        // Position in the user's list (1, 2, ...).
        public int Order { get; set; }

        public string ZoneSlug { get; set; } = string.Empty;

        // Name displayed when the user selected it ("Villa Luro, CABA"), so it can be listed without querying zones.
        public string Name { get; set; } = string.Empty;

        // Starting zone selected in the wizard.
        public bool IsInitial { get; set; }

        // Zone manually added to the automatic ranking.
        public bool IsAdded { get; set; }

        // Starting point the user marked inside the zone; null = the center of the zone.
        // Trips to the places are measured from here, so it changes the score.
        public double? OriginLatitude { get; set; }
        public double? OriginLongitude { get; set; }

        public bool HasOrigin => OriginLatitude.HasValue && OriginLongitude.HasValue;

        // Same zone for another analysis (a version or a recalculation)
        public AnalysisZone Copy() =>
            new()
            {
                Order = Order,
                ZoneSlug = ZoneSlug,
                Name = Name,
                IsInitial = IsInitial,
                IsAdded = IsAdded,
                OriginLatitude = OriginLatitude,
                OriginLongitude = OriginLongitude,
            };
    }
}
