namespace ZonaMatch.Domain.Scoring
{
    public enum Priority : short
    {
        Low = 1,
        Medium = 2,
        High = 3,
    }

    public enum CriterionDirection
    {
        HigherIsBetter,
        LowerIsBetter,
    }

    public sealed record WeightedCriterion(string Code, Priority Priority, CriterionDirection Direction)
    {
        public int Weight => (int)Priority;
    }

    // valores crudos de cada criterio para una zona; null = no hay dato para esa zona
    public sealed record ZoneCriterionValues(int ZoneId, IReadOnlyDictionary<string, double?> Values);

    public sealed record CriterionScore(string Code, double? Score);

    public sealed record ZoneScore(int ZoneId, double Score, double Coverage, IReadOnlyList<CriterionScore> Breakdown);

    // motor mínimo del ZonaMatch Score (PBI 34a/35a): normaliza cada criterio entre las zonas
    // comparadas y promedia según el peso de la prioridad, informando la cobertura del dato
    public static class ZoneScoreCalculator
    {
        // piso del puntaje relativo: la peor zona de un criterio no queda en cero
        public const double Floor = 40d;

        public static IReadOnlyList<ZoneScore> Calculate(
            IReadOnlyList<ZoneCriterionValues> zones,
            IReadOnlyList<WeightedCriterion> criteria)
        {
            ArgumentNullException.ThrowIfNull(zones);
            ArgumentNullException.ThrowIfNull(criteria);

            if (criteria.Count == 0)
            {
                throw new ArgumentException("Hay que elegir al menos un criterio.", nameof(criteria));
            }

            var ranges = criteria.ToDictionary(
                criterion => criterion.Code,
                criterion => Range(zones, criterion.Code));

            var totalWeight = criteria.Sum(criterion => criterion.Weight);

            return zones
                .Select(zone => ScoreZone(zone, criteria, ranges, totalWeight))
                .OrderByDescending(score => score.Score)
                .ThenBy(score => score.ZoneId)
                .ToList();
        }

        private static ZoneScore ScoreZone(
            ZoneCriterionValues zone,
            IReadOnlyList<WeightedCriterion> criteria,
            Dictionary<string, (double Min, double Max)?> ranges,
            int totalWeight)
        {
            var breakdown = new List<CriterionScore>(criteria.Count);
            var weightedSum = 0d;
            var coveredWeight = 0;

            foreach (var criterion in criteria)
            {
                var range = ranges[criterion.Code];
                var value = zone.Values.GetValueOrDefault(criterion.Code);

                if (value is null || range is null)
                {
                    breakdown.Add(new CriterionScore(criterion.Code, null));
                    continue;
                }

                var score = Normalize(value.Value, range.Value, criterion.Direction);
                breakdown.Add(new CriterionScore(criterion.Code, score));
                weightedSum += score * criterion.Weight;
                coveredWeight += criterion.Weight;
            }

            var total = coveredWeight == 0 ? 0d : Math.Round(weightedSum / coveredWeight, 1);
            var coverage = Math.Round((double)coveredWeight / totalWeight, 2);

            return new ZoneScore(zone.ZoneId, total, coverage, breakdown);
        }

        private static (double Min, double Max)? Range(IReadOnlyList<ZoneCriterionValues> zones, string code)
        {
            var values = zones
                .Select(zone => zone.Values.GetValueOrDefault(code))
                .OfType<double>()
                .ToList();

            return values.Count == 0 ? null : (values.Min(), values.Max());
        }

        private static double Normalize(double value, (double Min, double Max) range, CriterionDirection direction)
        {
            if (range.Max.Equals(range.Min))
            {
                return 100d;
            }

            var ratio = (value - range.Min) / (range.Max - range.Min);

            if (direction == CriterionDirection.LowerIsBetter)
            {
                ratio = 1d - ratio;
            }

            return Math.Round(Floor + (100d - Floor) * ratio, 1);
        }
    }
}
