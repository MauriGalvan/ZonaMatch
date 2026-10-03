using Microsoft.Extensions.Options;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.PointsOfInterest;

namespace ZonaMatch.Application.Ingestion
{
    // vincula el mismo lugar informado por dos fuentes (ej. escuela del padrón y de OSM)
    public class PoiDeduplicationService
    {
        private readonly IPoiRepository _pois;
        private readonly IngestionOptions _options;

        public PoiDeduplicationService(IPoiRepository pois, IOptions<IngestionOptions> options)
        {
            _pois = pois;
            _options = options.Value;
        }

        public async Task<DeduplicationReport> RunAsync(CancellationToken cancellationToken)
        {
            var matcher = new PoiDuplicateMatcher(_options.DuplicateMaxDistanceMeters);
            var candidates = await _pois.FindDuplicateCandidatesAsync(_options.DuplicateMaxDistanceMeters, cancellationToken);
            var duplicates = new List<PoiDuplicate>();
            var linked = new HashSet<long>();

            // los pares más cercanos primero; cada POI participa en un solo vínculo
            foreach (var candidate in candidates.OrderBy(candidate => candidate.DistanceMeters))
            {
                if (linked.Contains(candidate.First.Id) || linked.Contains(candidate.Second.Id))
                {
                    continue;
                }

                var duplicate = matcher.Match(candidate.First, candidate.Second);

                if (duplicate is null)
                {
                    continue;
                }

                duplicates.Add(duplicate);
                linked.Add(duplicate.DuplicateId);
                linked.Add(duplicate.CanonicalId);
            }

            if (duplicates.Count > 0)
            {
                await _pois.MarkDuplicatesAsync(duplicates, cancellationToken);
            }

            return new DeduplicationReport(candidates.Count, duplicates.Count);
        }
    }
}
