namespace ZonaMatch.Domain.Common
{
    // Aspects a neighbor rates in a review. They are also the topics a review can be about (review filters).
    public static class AspectoZona
    {
        public const string Seguridad = "seguridad";
        public const string Transporte = "transporte";
        public const string Conectividad = "conectividad";
        public const string Comercios = "comercios";
        public const string EspaciosVerdes = "espacios-verdes";

        public static readonly IReadOnlyList<string> Todos =
            [Seguridad, Transporte, Conectividad, Comercios, EspaciosVerdes];

        // Normalizes a list of codes (lowercase, no repeats, in the order of Todos).
        // Returns false when some code is unknown, naming it in the error.
        public static bool TryNormalizar(IEnumerable<string>? codigos, out IReadOnlyList<string> normalizados, out string error)
        {
            var pedidos = (codigos ?? [])
                .Select(codigo => codigo.Trim().ToLowerInvariant())
                .Where(codigo => codigo.Length > 0)
                .ToHashSet();

            var desconocidos = pedidos.Except(Todos).ToList();
            if (desconocidos.Count > 0)
            {
                normalizados = [];
                error = $"Tema desconocido: {string.Join(", ", desconocidos)}. Validos: {string.Join(", ", Todos)}.";
                return false;
            }

            normalizados = Todos.Where(pedidos.Contains).ToList();
            error = string.Empty;
            return true;
        }
    }
}
