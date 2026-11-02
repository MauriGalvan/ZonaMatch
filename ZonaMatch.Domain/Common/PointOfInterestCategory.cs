namespace ZonaMatch.Domain.Common;

/// <summary>Standard point-of-interest categories used by the application.</summary>
public static class PointOfInterestCategory
{
    // These values are part of the existing client contract and remain unchanged.
    public const string PublicTransport = "transporte";
    public const string Education = "educacion";
    public const string Healthcare = "salud";
    public const string Shopping = "comercios";
    public const string Sports = "deporte";
    public const string GreenSpaces = "espacios-verdes";
    public const string Culture = "cultura";

    public static readonly IReadOnlyList<string> All =
        [PublicTransport, Education, Healthcare, Shopping, Sports, GreenSpaces, Culture];

    /// <summary>Parses a comma-separated list. An empty list selects every category.</summary>
    public static bool TryParseList(string? list, out IReadOnlyList<string> categories, out string error)
    {
        error = string.Empty;

        var codes = (list ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (codes.Count == 0)
        {
            categories = All;
            return true;
        }

        var unknownCodes = codes.Where(code => !All.Contains(code)).ToList();
        if (unknownCodes.Count > 0)
        {
            categories = [];
            error = $"Categorias desconocidas: {string.Join(", ", unknownCodes)}. Validas: {string.Join(", ", All)}.";
            return false;
        }

        categories = codes;
        return true;
    }
}
