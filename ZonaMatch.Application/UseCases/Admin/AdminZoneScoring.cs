using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Admin;

// Scores a zone for the admin panel the same way the analysis does, with the five criteria at the same priority and
// no places or budget, so the result describes the zone itself and not a search.
internal static class AdminZoneScoring
{
    public const string SamePriority = "media";

    public static readonly IReadOnlyList<string> Criteria =
        [ScoringEngine.Safety, ScoringEngine.Mobility, ScoringEngine.GreenSpaces, ScoringEngine.Education, ScoringEngine.Health];

    private static readonly ScoringRequest Request = new(
        Criteria.ToDictionary(criterion => criterion, _ => SamePriority),
        Places: [],
        MaximumMonthlyBudget: null);

    public static ZoneScoreResult Score(ScoringEngine engine, ZoneProfile zone) => engine.Calculate(zone, Request);

    // Every credit-eligible sale of the zone (every type and size); null without credit data.
    public static int? CreditListings(ZoneProfile zone) =>
        zone.CreditSalesBySegment.TryGetValue((HousingTypes.All, 0), out var sales) ? sales.Listings : null;

    // Price at which half of the listings are cheaper, read between the two points around it. The distribution starts at
    // price 0 with no listings. Null without prices.
    public static decimal? MedianPrice(CreditSales sales)
    {
        if (sales.Distribution is not { Shares: { } shares } distribution)
            return null;

        for (var i = 1; i < shares.Length; i++)
        {
            if (shares[i] < 0.5m)
                continue;

            var span = shares[i] - shares[i - 1];
            var inside = span > 0 ? (0.5m - shares[i - 1]) / span : 0;
            return Math.Round(distribution.Prices[i - 1] + (distribution.Prices[i] - distribution.Prices[i - 1]) * inside);
        }

        return distribution.Maximum;
    }
}
