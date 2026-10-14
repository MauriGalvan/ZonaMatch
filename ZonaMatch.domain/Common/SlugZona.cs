using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZonaMatch.Domain.Common
{
    // URL identifier of a zone, built from its name: "Villa Luro" -> "villa-luro", "Ñuñoa" -> "nunoa".
    // The zone repository applies the same rule in SQL (keep both in sync).
    public static partial class SlugZona
    {
        public static string Generar(string nombre)
        {
            var sinAcentos = new StringBuilder(nombre.Length);
            foreach (var c in nombre.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sinAcentos.Append(c);
            }

            return NoAlfanumerico().Replace(sinAcentos.ToString(), "-").Trim('-');
        }

        // Lowercase ASCII words joined by single hyphens
        public static bool EsValido(string? slug) => slug is not null && Formato().IsMatch(slug);

        [GeneratedRegex("[^a-z0-9]+")]
        private static partial Regex NoAlfanumerico();

        [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
        private static partial Regex Formato();
    }
}
