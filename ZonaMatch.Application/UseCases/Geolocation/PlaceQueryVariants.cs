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

        // Acronyms of well-known places (taken from the Nominatim "short_name" list). Photon does not index
        // short_name, so a standalone word that is one of these is replaced by the full name. Words that are also
        // common Spanish words ("uno") are left out on purpose.
        private static readonly Dictionary<string, string> Siglas = new(StringComparer.OrdinalIgnoreCase)
        {
            ["unla"] = "universidad nacional de lanús",
            ["unlam"] = "universidad nacional de la matanza",
            ["unlz"] = "universidad nacional de lomas de zamora",
            ["undav"] = "universidad nacional de avellaneda",
            ["ungs"] = "universidad nacional de general sarmiento",
            ["unpaz"] = "universidad nacional de josé clemente paz",
            ["unsam"] = "universidad nacional de san martín",
            ["uba"] = "universidad de buenos aires",
            ["uca"] = "universidad católica argentina",
            ["uai"] = "universidad abierta interamericana",
            ["utn"] = "universidad tecnológica nacional",
            ["itba"] = "instituto tecnológico de buenos aires",
            ["fadu"] = "facultad de arquitectura, diseño y urbanismo",
            ["fcen"] = "facultad de ciencias exactas y naturales",
            ["higa"] = "hospital interzonal general de agudos",
            ["inti"] = "instituto nacional de tecnología industrial",
            ["indec"] = "instituto nacional de estadística y censos",
            ["anses"] = "administración nacional de la seguridad social",
            ["bna"] = "banco de la nación argentina",
            ["smn"] = "servicio meteorológico nacional",
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

        // For typo-tolerant, search-as-you-type engines (Photon). They need no retries as typed, but they do not know
        // abbreviations ("Av. Gral") or filler words ("nro"), which never appear in OSM names: the cleaned query goes
        // first and, only if it finds nothing, the street without its number (approximate).
        public static IReadOnlyList<(string Query, bool IsApproximate)> BuildForTolerantEngines(string search)
        {
            var original = Espacios().Replace(search.Trim(), " ");
            var cleaned = Normalize(original, keepLastWordAsTyped: true);
            var variants = new List<(string Query, bool IsApproximate)>();

            void Add(string query, bool approximate)
            {
                var clean = Espacios().Replace(query, " ").Trim(' ', ',');
                if (clean.Length > 0 && !variants.Any(variant => string.Equals(variant.Query, clean, StringComparison.OrdinalIgnoreCase)))
                    variants.Add((clean, approximate));
            }

            var main = cleaned.Length > 0 ? cleaned : original;

            // "unla" -> "universidad nacional de lanús" first; the query as typed stays as a fallback
            var expanded = ExpandSiglas(main);
            if (!string.Equals(expanded, main, StringComparison.Ordinal))
                Add(expanded, false);

            Add(main, false);

            var firstPart = cleaned.Split(',')[0];
            if (Numero().IsMatch(firstPart))
                Add(Numero().Replace(firstPart, " "), true);

            return variants;
        }

        private static string ExpandSiglas(string query)
        {
            var words = query.Split(' ');
            return string.Join(' ', words.Select(word =>
            {
                var bare = word.TrimEnd(',');
                var trailing = word[bare.Length..];
                return Siglas.TryGetValue(bare, out var full) ? full + trailing : word;
            }));
        }

        // keepLastWordAsTyped: the last word may still be half written ("hosp" -> "hospital"), so it is not expanded
        // unless it ends with a dot ("hosp.").
        private static string Normalize(string search, bool keepLastWordAsTyped = false)
        {
            var words = Separadores().Split(search.ToLowerInvariant())
                .Where(word => word.Length > 0 && !Relleno.Contains(word))
                .ToList();

            var expanded = words.Select((word, index) =>
            {
                var bare = word.TrimEnd('.');
                var stillTyping = keepLastWordAsTyped && index == words.Count - 1 && !word.EndsWith('.');
                return !stillTyping && Abreviaturas.TryGetValue(bare, out var full) ? full : bare;
            });

            return string.Join(' ', expanded).Replace(" ,", ",");
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
