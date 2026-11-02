using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZonaMatch.Domain.Common;

/// <summary>Creates and validates URL slugs for zones.</summary>
public static partial class ZoneSlug
{
    // Keep this normalization aligned with the zone repository's SQL search logic.
    public static string Create(string name)
    {
        var normalizedName = new StringBuilder(name.Length);
        foreach (var character in name.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                normalizedName.Append(character);
        }

        return NonAlphanumeric().Replace(normalizedName.ToString(), "-").Trim('-');
    }

    // Slugs contain lowercase ASCII words joined by single hyphens.
    public static bool IsValid(string? slug) => slug is not null && Format().IsMatch(slug);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Format();
}
