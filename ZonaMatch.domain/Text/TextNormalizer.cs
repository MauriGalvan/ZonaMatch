using System.Globalization;
using System.Text;

namespace ZonaMatch.Domain.Text
{
    // normaliza nombres que llegan escritos distinto según la fuente
    // ("RAMOS MEJIA", "Ramos Mejía", "ramos  mejía") para buscarlos y compararlos
    public static class TextNormalizer
    {
        private static readonly HashSet<string> LowercaseConnectors = new(StringComparer.Ordinal)
        {
            "de", "del", "la", "las", "los", "el", "y", "e",
        };

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var previousWasSpace = false;

            foreach (var character in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToUpperInvariant(character));
                    previousWasSpace = false;
                }
                else if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().TrimEnd();
        }

        public static string Slugify(params string?[] parts)
        {
            var normalized = string.Join(' ', parts.Select(Normalize).Where(part => part.Length > 0));
            return normalized.Replace(' ', '-').ToLowerInvariant();
        }

        // "LA MATANZA" -> "La Matanza"; respeta nombres que ya vienen con mayúsculas y minúsculas
        public static string ToDisplayName(string value)
        {
            var trimmed = value.Trim();

            if (trimmed.Any(char.IsLower))
            {
                return trimmed;
            }

            var words = trimmed.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (var index = 0; index < words.Length; index++)
            {
                if (index > 0 && LowercaseConnectors.Contains(words[index]))
                {
                    continue;
                }

                words[index] = char.ToUpperInvariant(words[index][0]) + words[index][1..];
            }

            return string.Join(' ', words);
        }
    }
}
