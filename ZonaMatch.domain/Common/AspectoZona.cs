namespace ZonaMatch.Domain.Common
{
    // Aspectos que un vecino puntua en una resena. Tambien son los temas de los que puede hablar una resena (filtros de resenas).
    public static class AspectoZona
    {
        public const string Seguridad = "seguridad";
        public const string Transporte = "transporte";
        public const string Conectividad = "conectividad";
        public const string Comercios = "comercios";
        public const string EspaciosVerdes = "espacios-verdes";

        public static readonly IReadOnlyList<string> Todos =
            [Seguridad, Transporte, Conectividad, Comercios, EspaciosVerdes];

        // Normaliza una lista de codigos (minusculas, sin repetidos, en el orden de Todos).
        // Devuelve false si algun codigo es desconocido y lo nombra en el error.
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
