namespace ZonaMatch.Domain.Puntaje
{
    // Distribucion de precios de una zona: el precio en cada decimo de las publicaciones (11 valores, del mas barato
    // al mas caro de la muestra recortada). El precio medio es el del medio: la mitad de los avisos cuesta menos.
    public static class Distribucion
    {
        public const int Valores = 11;

        public static bool EsValida(decimal[]? deciles) => deciles is { Length: Valores };

        public static decimal Minimo(decimal[] deciles) => deciles[0];

        public static decimal Maximo(decimal[] deciles) => deciles[^1];

        public static decimal Medio(decimal[] deciles) => deciles[Valores / 2];

        // Porcentaje de publicaciones que cuestan hasta el tope (0 a 100), interpolando entre los deciles
        public static int PorcentajeHasta(decimal[] deciles, decimal tope)
        {
            if (tope < deciles[0])
                return 0;
            if (tope >= deciles[^1])
                return 100;

            for (var i = 1; i < deciles.Length; i++)
            {
                if (tope >= deciles[i])
                    continue;

                var tramo = deciles[i] - deciles[i - 1];
                var dentro = tramo > 0 ? (tope - deciles[i - 1]) / tramo : 1;
                return (int)Math.Round((i - 1 + dentro) * 100 / (deciles.Length - 1), MidpointRounding.AwayFromZero);
            }

            return 100;
        }

        // Cada valor por un factor y mas un monto fijo (la superficie, las expensas)
        public static decimal[] Escalar(decimal[] deciles, decimal factor, decimal suma = 0) =>
            deciles.Select(valor => Math.Round(valor * factor + suma)).ToArray();
    }
}
