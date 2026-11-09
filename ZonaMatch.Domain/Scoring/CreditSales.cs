namespace ZonaMatch.Domain.Scoring
{
    // Sales eligible for a mortgage ("apto crédito") of one property type and room group of a zone (Zonaprop): how many
    // are published and the share of them priced up to a few dollar prices. Listings is null in data loaded before the
    // counts were stored.
    public sealed record CreditSales(int? Listings, decimal[]? PricesUsd, decimal[]? Shares)
    {
        // Zonaprop has none published in this segment
        public bool IsEmpty => Listings == 0;

        // Starts at 0 at price 0, since the first price read already leaves some listings below it. Null without prices.
        public PriceDistribution? Distribution =>
            PriceDistribution.From(PricesUsd, Shares) is null
                ? null
                : new PriceDistribution([0, .. PricesUsd!], [0, .. Shares!]);

        // Usable for scoring: either known to be empty or with its prices
        public bool HasData => IsEmpty || Distribution is not null;
    }

    // The segment credit-eligible sales were taken from: Type is a HousingTypes value or All, Rooms 0 for every size.
    public sealed record CreditSegment(string Type, int Rooms, CreditSales Sales);
}
