namespace ZonaMatch.Domain.Common
{
    // Standard point-of-interest categories shown in the app (layers of the zone map)
    public static class CategoriaPuntoInteres
    {
        public const string Transporte = "transporte";
        public const string Educacion = "educacion";
        public const string Salud = "salud";
        public const string Comercios = "comercios";
        public const string Deporte = "deporte";
        public const string EspaciosVerdes = "espacios-verdes";
        public const string Cultura = "cultura";

        public static readonly IReadOnlyList<string> Todas =
            [Transporte, Educacion, Salud, Comercios, Deporte, EspaciosVerdes, Cultura];

        // Parses a comma-separated list ("transporte,salud"). Empty means every category.
        // Returns false when some code is unknown, naming it in the error.
        public static bool TryParseLista(string? lista, out IReadOnlyList<string> categorias, out string error)
        {
            error = string.Empty;

            var codigos = (lista ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(codigo => codigo.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (codigos.Count == 0)
            {
                categorias = Todas;
                return true;
            }

            var desconocidas = codigos.Where(codigo => !Todas.Contains(codigo)).ToList();

            if (desconocidas.Count > 0)
            {
                categorias = [];
                error = $"Categorias desconocidas: {string.Join(", ", desconocidas)}. Validas: {string.Join(", ", Todas)}.";
                return false;
            }

            categorias = codigos;
            return true;
        }
    }
}
