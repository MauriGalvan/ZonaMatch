namespace ZonaMatch.Application.Services
{
    // How the community shows who wrote something. Users have no name yet, only an email, which must not be
    // exposed: the initials come from the part before the "@" ("ana.perez@..." -> "AP", "juan@..." -> "JU").
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
