namespace ZonaMatch.Domain.Scoring
{
    public enum RestrictionStatus
    {
        Complies,
        NearLimit,
        Exceeds,
        Unknown,
    }

    // restricciones duras (PBI 28a): una zona que excede el límite se marca, no se oculta
    public static class RestrictionEvaluator
    {
        // a partir del 90 % del máximo la zona queda "cerca del límite"
        public const double NearLimitRatio = 0.9d;

        public static RestrictionStatus Evaluate(double? actual, double limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "El límite debe ser positivo.");
            }

            if (actual is null)
            {
                return RestrictionStatus.Unknown;
            }

            var ratio = actual.Value / limit;

            if (ratio > 1d)
            {
                return RestrictionStatus.Exceeds;
            }

            return ratio >= NearLimitRatio ? RestrictionStatus.NearLimit : RestrictionStatus.Complies;
        }
    }

    // combinación de perfiles en búsquedas en grupo (PBI 58a)
    public static class ProfileCombiner
    {
        public static double Combine(IReadOnlyList<(double Weight, double Score)> participants)
        {
            ArgumentNullException.ThrowIfNull(participants);

            var totalWeight = participants.Sum(participant => participant.Weight);

            if (participants.Count == 0 || totalWeight <= 0)
            {
                throw new ArgumentException("Se necesita al menos un participante con peso positivo.", nameof(participants));
            }

            return Math.Round(participants.Sum(participant => participant.Weight * participant.Score) / totalWeight, 1);
        }
    }
}
