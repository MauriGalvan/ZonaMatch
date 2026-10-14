namespace ZonaMatch.Application.UseCases.Comunidad
{
    // Como muestra la comunidad quien escribio algo. Los usuarios todavia no tienen nombre, solo email, que no
    // se debe exponer: las iniciales salen de lo anterior a la "@" ("ana.perez@..." -> "AP", "juan@..." -> "JU").
    public static class Autor
    {
        private static readonly char[] Separadores = ['.', '_', '-', '+'];

        public static string Iniciales(string? email)
        {
            var local = (email ?? string.Empty).Split('@')[0];
            var partes = local.Split(Separadores, StringSplitOptions.RemoveEmptyEntries)
                .Where(parte => char.IsLetter(parte[0]))
                .ToList();

            var iniciales = partes.Count switch
            {
                0 => "?",
                1 => partes[0].Length > 1 && char.IsLetter(partes[0][1]) ? partes[0][..2] : partes[0][..1],
                _ => $"{partes[0][0]}{partes[1][0]}"
            };

            return iniciales.ToUpperInvariant();
        }
    }
}
