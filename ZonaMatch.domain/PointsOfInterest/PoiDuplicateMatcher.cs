using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Geo;
using ZonaMatch.Domain.Sources;

namespace ZonaMatch.Domain.PointsOfInterest
{
    // datos mínimos para decidir si dos POIs de fuentes distintas son el mismo lugar
    public sealed record PoiMatchCandidate(long Id, string NormalizedName, Point Location, DataSourceKind SourceKind);

    public sealed record PoiDuplicate(long DuplicateId, long CanonicalId);

    public class PoiDuplicateMatcher
    {
        // palabras genéricas que no distinguen un establecimiento de otro
        private static readonly HashSet<string> GenericWords = new(StringComparer.Ordinal)
        {
            "ESCUELA", "EDUCACION", "PRIMARIA", "SECUNDARIA", "INICIAL", "JARDIN", "INFANTES", "MATERNAL",
            "COLEGIO", "INSTITUTO", "EP", "ES", "EES", "EEP", "JI", "N", "NO", "NRO", "NUMERO",
            "DE", "DEL", "LA", "EL", "LOS", "LAS", "Y",
        };

        // si ninguno de los dos nombres tiene palabras distintivas, solo cuentan como iguales muy cerca
        private const double SameSpotMeters = 30d;
        private const double MinimumNameSimilarity = 0.5d;

        private readonly double _maxDistanceMeters;

        public PoiDuplicateMatcher(double maxDistanceMeters)
        {
            if (maxDistanceMeters <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistanceMeters), maxDistanceMeters, "La distancia debe ser positiva.");
            }

            _maxDistanceMeters = maxDistanceMeters;
        }

        public PoiDuplicate? Match(PoiMatchCandidate first, PoiMatchCandidate second)
        {
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);

            var distance = GeoMath.DistanceMeters(first.Location, second.Location);

            if (distance > _maxDistanceMeters || !NamesMatch(first.NormalizedName, second.NormalizedName, distance))
            {
                return null;
            }

            // gana la fuente oficial; a igual tipo de fuente, el registro más antiguo
            var firstWins = first.SourceKind == second.SourceKind
                ? first.Id < second.Id
                : first.SourceKind == DataSourceKind.Official;

            return firstWins
                ? new PoiDuplicate(second.Id, first.Id)
                : new PoiDuplicate(first.Id, second.Id);
        }

        private static bool NamesMatch(string firstName, string secondName, double distance)
        {
            var first = DistinctiveTokens(firstName);
            var second = DistinctiveTokens(secondName);

            if (first.Count == 0 || second.Count == 0)
            {
                return distance <= SameSpotMeters;
            }

            var firstNumbers = first.Where(IsNumber).ToHashSet();
            var secondNumbers = second.Where(IsNumber).ToHashSet();

            // "Escuela N° 39" y "EP N 39 El Pampero" son la misma si comparten el número
            if (firstNumbers.Count > 0 && secondNumbers.Count > 0)
            {
                return firstNumbers.Overlaps(secondNumbers);
            }

            var intersection = first.Intersect(second).Count();
            var union = first.Union(second).Count();
            return (double)intersection / union >= MinimumNameSimilarity;
        }

        private static HashSet<string> DistinctiveTokens(string normalizedName)
        {
            return normalizedName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => IsNumber(token) ? token.TrimStart('0') : token)
                .Where(token => token.Length > 0 && !GenericWords.Contains(token))
                .ToHashSet(StringComparer.Ordinal);
        }

        private static bool IsNumber(string token) => token.All(char.IsDigit);
    }
}
