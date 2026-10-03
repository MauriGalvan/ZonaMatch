namespace ZonaMatch.Domain.PointsOfInterest
{
    // aplica las reglas de mapeo configuradas: agregar una categoría no requiere código (PBI 12)
    public class PoiClassifier
    {
        private readonly ILookup<short, PoiMappingRule> _rulesBySource;

        public PoiClassifier(IEnumerable<PoiMappingRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            _rulesBySource = rules
                .OrderBy(rule => rule.Priority)
                .ToLookup(rule => rule.DataSourceId);
        }

        // devuelve la subcategoría de la regla de menor prioridad que coincide, o null si ninguna aplica
        public short? Classify(short dataSourceId, IReadOnlyDictionary<string, string> attributes)
        {
            ArgumentNullException.ThrowIfNull(attributes);

            foreach (var rule in _rulesBySource[dataSourceId])
            {
                if (attributes.TryGetValue(rule.Attribute, out var value)
                    && string.Equals(value.Trim(), rule.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.CategoryId;
                }
            }

            return null;
        }
    }
}
