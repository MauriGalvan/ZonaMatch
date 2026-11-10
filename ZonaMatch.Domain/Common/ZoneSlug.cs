using System.Text.RegularExpressions;

namespace ZonaMatch.Domain.Common;

/// <summary>Validates zone URL slugs.</summary>
/// <remarks>
/// Slugs are created in a single place, the geo.slug_zona SQL function of etl/calculate_zones.sql, and kept for
/// each OSM id in geo.zona_slugs. The zone search turns the search text into a slug with that same function.
/// </remarks>
public static partial class ZoneSlug
{
    // Slugs contain lowercase ASCII words joined by single hyphens.
    public static bool IsValid(string? slug) => slug is not null && Format().IsMatch(slug);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Format();
}
