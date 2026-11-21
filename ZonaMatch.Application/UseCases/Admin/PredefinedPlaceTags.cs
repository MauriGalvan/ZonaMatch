namespace ZonaMatch.Application.UseCases.Admin;

// Tags the analysis wizard offers for a place (front: src/features/site-analysis/data/poi-tags.json). Places store the
// tag label; any other label is a custom tag the user typed. Keep both lists in sync.
public static class PredefinedPlaceTags
{
    public static readonly IReadOnlyList<string> Labels =
        ["Trabajo", "Estudio", "Colegio", "Clínica / obra social", "Casa de amigo", "Club / deporte"];

    private static readonly HashSet<string> Normalized = Labels.Select(Normalize).ToHashSet();

    public static bool IsPredefined(string tag) => Normalized.Contains(Normalize(tag));

    // Same tag whatever its case or surrounding spaces ("Workshop " and "workshop").
    public static string Normalize(string tag) => tag.Trim().ToLowerInvariant();
}
