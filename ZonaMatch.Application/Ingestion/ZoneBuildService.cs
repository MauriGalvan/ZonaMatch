using Microsoft.Extensions.Options;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Application.Ingestion
{
    // arma la lista de zonas de análisis: barrios de CABA y localidades de los partidos en alcance
    public class ZoneBuildService
    {
        private readonly ITerritorialUnitRepository _units;
        private readonly IZoneRepository _zones;
        private readonly IngestionOptions _options;

        public ZoneBuildService(ITerritorialUnitRepository units, IZoneRepository zones, IOptions<IngestionOptions> options)
        {
            _units = units;
            _zones = zones;
            _options = options.Value;
        }

        public async Task<ZoneBuildReport> RebuildAsync(CancellationToken cancellationToken)
        {
            var partidos = (await _units.GetByTypesAsync([TerritorialUnitType.Partido], cancellationToken))
                .ToDictionary(partido => partido.Id);
            var candidates = await _units.GetByTypesAsync(
                [TerritorialUnitType.Barrio, TerritorialUnitType.Localidad],
                cancellationToken);
            var scope = _options.ScopePartidoCodes.ToHashSet(StringComparer.Ordinal);

            var zones = new List<Zone>();
            var slugCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var unit in candidates.OrderBy(unit => unit.Id))
            {
                string? partidoName = null;

                if (unit.Type == TerritorialUnitType.Localidad)
                {
                    if (unit.ParentId is null
                        || !partidos.TryGetValue(unit.ParentId.Value, out var partido)
                        || (scope.Count > 0 && !scope.Contains(partido.Code ?? string.Empty)))
                    {
                        continue;
                    }

                    partidoName = partido.Name;
                }

                var zone = Zone.Create(unit.Id, unit.Type, unit.Name, partidoName);

                // dos localidades homónimas en el mismo partido no pueden compartir slug
                var seen = slugCounts.GetValueOrDefault(zone.Slug);
                slugCounts[zone.Slug] = seen + 1;

                if (seen > 0)
                {
                    zone.DisambiguateSlug(seen + 1);
                }

                zones.Add(zone);
            }

            await _zones.SyncAsync(zones, cancellationToken);
            var adjacencies = await _zones.RebuildAdjacenciesAsync(cancellationToken);

            return new ZoneBuildReport(zones.Count, adjacencies);
        }
    }
}
