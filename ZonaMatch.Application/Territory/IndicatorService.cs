using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.Territory
{
    // si la zona no tiene el dato propio, lo hereda de su comuna/partido y lo marca (PBI 21, 37a)
    public class IndicatorService
    {
        private readonly IIndicatorRepository _indicators;

        public IndicatorService(IIndicatorRepository indicators)
        {
            _indicators = indicators;
        }

        public async Task<IReadOnlyDictionary<int, IReadOnlyDictionary<string, IndicatorValueDto>>> ResolveAsync(
            IReadOnlyList<ZoneData> zones,
            IReadOnlyCollection<string> indicatorCodes,
            CancellationToken cancellationToken)
        {
            var unitIds = zones
                .SelectMany(zone => new[] { zone.TerritorialUnitId, zone.ParentUnitId })
                .OfType<long>()
                .Distinct()
                .ToList();

            var data = indicatorCodes.Count == 0 || unitIds.Count == 0
                ? []
                : await _indicators.GetAsync(unitIds, indicatorCodes, cancellationToken);

            // el dato más reciente de cada unidad e indicador
            var latest = data
                .GroupBy(item => (item.TerritorialUnitId, item.IndicatorCode))
                .ToDictionary(group => group.Key, group => group.MaxBy(item => item.ReferenceDate)!);

            var result = new Dictionary<int, IReadOnlyDictionary<string, IndicatorValueDto>>();

            foreach (var zone in zones)
            {
                var values = new Dictionary<string, IndicatorValueDto>(StringComparer.Ordinal);

                foreach (var code in indicatorCodes)
                {
                    if (latest.TryGetValue((zone.TerritorialUnitId, code), out var own))
                    {
                        values[code] = ToDto(own, inherited: false, zone.Name);
                    }
                    else if (zone.ParentUnitId is not null && latest.TryGetValue((zone.ParentUnitId.Value, code), out var parent))
                    {
                        values[code] = ToDto(parent, inherited: true, zone.ParentUnitName ?? zone.ParentName);
                    }
                }

                result[zone.Id] = values;
            }

            return result;
        }

        private static IndicatorValueDto ToDto(IndicatorData data, bool inherited, string level) =>
            new(data.IndicatorCode, data.Value, data.Unit, data.SourceName, data.ReferenceDate, inherited, level);
    }
}
