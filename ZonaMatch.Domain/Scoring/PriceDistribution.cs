namespace ZonaMatch.Domain.Scoring
{
    // Price distribution of a zone: the price at every tenth of the listings (11 values, from the cheapest to the most
    // expensive listing). The median is the middle value: half of the listings cost less.
    public static class PriceDistribution
    {
        public const int Values = 11;

        public static bool IsValid(decimal[]? deciles) => deciles is { Length: Values };

        public static decimal Minimum(decimal[] deciles) => deciles[0];

        public static decimal Maximum(decimal[] deciles) => deciles[^1];

        public static decimal Median(decimal[] deciles) => deciles[Values / 2];

        // Share of the listings priced up to the limit (0 to 100), interpolating between the deciles.
        public static int PercentageUpTo(decimal[] deciles, decimal limit)
        {
            if (limit < deciles[0])
                return 0;
            if (limit >= deciles[^1])
                return 100;

            for (var i = 1; i < deciles.Length; i++)
            {
                if (limit >= deciles[i])
                    continue;

                var span = deciles[i] - deciles[i - 1];
                var inside = span > 0 ? (limit - deciles[i - 1]) / span : 1;
                return (int)Math.Round((i - 1 + inside) * 100 / (deciles.Length - 1), MidpointRounding.AwayFromZero);
            }

            return 100;
        }

        // Every value times a factor plus a fixed amount (the area, the building expenses).
        public static decimal[] Scale(decimal[] deciles, decimal factor, decimal addend = 0) =>
            deciles.Select(value => Math.Round(value * factor + addend)).ToArray();
    }
}
