namespace ZonaMatch.Domain.Scoring
{
    // Price distribution of a zone: prices at evenly spaced shares of the listings (21 points, one every 5 %; older
    // data has 11), from the cheapest to the most expensive listing, and the real share of the listings (0 to 1) priced
    // up to each of them, ties included. Many listings sit on round prices, so the share jumps there: with Shares a cap
    // on one of those prices counts them all. Without Shares the points are taken as evenly spaced.
    public sealed record PriceDistribution(decimal[] Prices, decimal[]? Shares = null)
    {
        public const int MinimumPoints = 2;

        public static bool IsValid(decimal[]? prices) => prices is { Length: >= MinimumPoints };

        // Null unless there are enough prices; shares that do not match the prices are ignored.
        public static PriceDistribution? From(decimal[]? prices, decimal[]? shares) =>
            IsValid(prices) ? new PriceDistribution(prices!, shares?.Length == prices!.Length ? shares : null) : null;

        public decimal Minimum => Prices[0];

        public decimal Maximum => Prices[^1];

        // The middle point: half of the listings cost less.
        public decimal Median => Prices[Prices.Length / 2];

        // Share of the listings priced up to the limit (0 to 100), interpolating between the points.
        public int PercentageUpTo(decimal limit)
        {
            if (limit < Prices[0])
                return 0;
            // At or above the last point, its share: 100 % when it is the most expensive listing, less when the points
            // stop before it (credit-eligible sales are read up to a few prices only)
            if (limit >= Prices[^1])
                return (int)Math.Round(ShareAt(Prices.Length - 1) * 100, MidpointRounding.AwayFromZero);

            for (var i = 1; i < Prices.Length; i++)
            {
                if (limit >= Prices[i])
                    continue;

                // Prices[i - 1] <= limit < Prices[i]: exact on a point, linear between two of them
                var span = Prices[i] - Prices[i - 1];
                var inside = span > 0 ? (limit - Prices[i - 1]) / span : 0;
                var share = ShareAt(i - 1) + (ShareAt(i) - ShareAt(i - 1)) * inside;
                return (int)Math.Round(share * 100, MidpointRounding.AwayFromZero);
            }

            return 100;
        }

        // Every price times a factor plus a fixed amount (the area, the building expenses); the shares stay.
        public PriceDistribution Scale(decimal factor, decimal addend = 0) =>
            this with { Prices = Prices.Select(price => Math.Round(price * factor + addend)).ToArray() };

        private decimal ShareAt(int index) => Shares?[index] ?? (decimal)index / (Prices.Length - 1);
    }
}
