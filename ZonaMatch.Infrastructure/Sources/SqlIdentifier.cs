using System.Text.RegularExpressions;

namespace ZonaMatch.Infrastructure.Sources
{
    // los nombres de tablas y columnas vienen de configuración: se validan y se citan
    public static partial class SqlIdentifier
    {
        // admite ':' porque osm2pgsql crea columnas como "addr:housenumber"
        [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_:]*$")]
        private static partial Regex Part();

        public static string Quote(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("El identificador SQL no puede estar vacío.", nameof(identifier));
            }

            var parts = identifier.Split('.');

            if (parts.Length > 2 || parts.Any(part => !Part().IsMatch(part)))
            {
                throw new ArgumentException($"Identificador SQL inválido: '{identifier}'.", nameof(identifier));
            }

            return string.Join('.', parts.Select(part => $"\"{part}\""));
        }
    }
}
