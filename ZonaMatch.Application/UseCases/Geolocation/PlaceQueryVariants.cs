using System.Text.RegularExpressions;

namespace ZonaMatch.Application.UseCases.Geolocation
{
    // Nominatim only matches what is written almost literally. These are the queries tried in order
    // until one returns places: as typed, normalized (abbreviations, "nro", extra words), and the street
    // without its number (a nearby result, flagged as approximate).
    public static partial class PlaceQueryVariants
    {
        private static readonly Dictionary<string, string> Abreviaturas = new(StringComparer.OrdinalIgnoreCase)
        {
            ["av"] = "avenida",
            ["avda"] = "avenida",
            ["avd"] = "avenida",
            ["gral"] = "general",
            ["pte"] = "presidente",
            ["pres"] = "presidente",
            ["dr"] = "doctor",
            ["cnel"] = "coronel",
            ["tte"] = "teniente",
            ["ing"] = "ingeniero",
            ["pje"] = "pasaje",
            ["psje"] = "pasaje",
            ["diag"] = "diagonal",
            ["bv"] = "bulevar",
            ["bvd"] = "bulevar",
            ["bvar"] = "bulevar",
            ["sta"] = "santa",
            ["sto"] = "santo",
            ["univ"] = "universidad",
            ["fac"] = "facultad",
            ["hosp"] = "hospital",
            ["esc"] = "escuela",
        };

        // Words that never help Nominatim find an address
        private static readonly HashSet<string> Relleno = new(StringComparer.OrdinalIgnoreCase)
        {
            "calle", "nro", "nº", "n°", "numero", "número", "altura", "al",
        };

        public static IReadOnlyList<(string Query, bool IsApproximate)> Build(string search)
        {
            var original = Espacios().Replace(search.Trim(), " ");
            var variants = new List<(string Query, bool IsApproximate)>();

            void Add(string query, bool approximate)
            {
                var clean = Espacios().Replace(query, " ").Trim(' ', ',');
                if (clean.Length > 0 && !variants.Any(variant => string.Equals(variant.Query, clean, StringComparison.OrdinalIgnoreCase)))
                    variants.Add((clean, approximate));
            }

            Add(original, false);

            var normalized = Normalize(original);
            Add(normalized, false);

            // "Maipú 4493, Vicente López": the part before the first comma usually finds it on its own
            var firstPart = normalized.Split(',')[0];
            Add(firstPart, false);

            // Without the house number the street is still found: close to the address, not exact
            if (Numero().IsMatch(firstPart))
                Add(Numero().Replace(firstPart, " "), true);

            return variants;
        }

        private static string Normalize(string search)
        {
            var words = Separadores().Split(search.ToLowerInvariant())
                .Where(word => word.Length > 0 && !Relleno.Contains(word))
                .Select(word => Abreviaturas.TryGetValue(word.TrimEnd('.'), out var full) ? full : word.TrimEnd('.'));

            return string.Join(' ', words).Replace(" ,", ",");
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex Espacios();

        // Splits on spaces and symbols but keeps commas as their own word, to know where the locality starts
        [GeneratedRegex(@"\s+|(?=,)|(?<=,)|[#()/;:]")]
        private static partial Regex Separadores();

        [GeneratedRegex(@"\b\d+\b")]
        private static partial Regex Numero();
    }
}
