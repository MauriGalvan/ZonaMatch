namespace ZonaMatch.Domain.Entities
{
    // A user's saved analysis (app.analisis table): compared zones and the criteria used,
    // copied from the profile when saved. A version derives from an original analysis and keeps
    // criteria adjusted for that case only; OriginalAnalysisId points to the original.
    public class Analysis
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UserId { get; set; }

        // Name shown in the user's list, for example "Near work".
        public string Name { get; set; } = string.Empty;

        // Criteria used for scoring (a Context of type analisis, owned by this analysis).
        public Guid ContextId { get; set; }

        public Context Context { get; set; } = null!;

        // Snapshot of the weighted result when the analysis was generated from a group.
        public string? GroupScoresJson { get; set; }

        // Result of a personal analysis when it was saved or recalculated (zones, scores, limits, trips and
        // recommendations): opening it shows the same, and versions can be compared. Null for analyses saved before
        // it existed or whose zones had no data: they are scored live.
        public string? ResultJson { get; set; }

        public DateTimeOffset? ResultGeneratedAt { get; set; }

        // Starred in the user's "Mis análisis" list; starred analyses appear first in "Favoritos".
        public bool IsFavorite { get; set; }

        // Null for originals; for a version, the original analysis it derives from.
        public Guid? OriginalAnalysisId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        // app.analisis_zonas rows, ordered as selected by the user.
        public ICollection<AnalysisZone> Zones { get; set; } = new List<AnalysisZone>();
    }
}
