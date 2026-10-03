using Microsoft.Extensions.Options;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.PointsOfInterest;

namespace ZonaMatch.Application.Ingestion
{
    // unifica POIs de fuentes heterogéneas (padrón educativo, OSM, ...) en un único formato
    public class PoiImportService
    {
        private readonly IPoiSourceReader _reader;
        private readonly IPoiRepository _pois;
        private readonly IPoiCatalogRepository _catalog;
        private readonly IDataSourceRepository _dataSources;
        private readonly TimeProvider _clock;
        private readonly IngestionOptions _options;

        public PoiImportService(
            IPoiSourceReader reader,
            IPoiRepository pois,
            IPoiCatalogRepository catalog,
            IDataSourceRepository dataSources,
            TimeProvider clock,
            IOptions<IngestionOptions> options)
        {
            _reader = reader;
            _pois = pois;
            _catalog = catalog;
            _dataSources = dataSources;
            _clock = clock;
            _options = options.Value;
        }

        public async Task<ImportReport> ImportAsync(CancellationToken cancellationToken)
        {
            var categories = await _catalog.GetCategoriesAsync(cancellationToken);
            var categoryNames = categories.ToDictionary(category => category.Id, category => category.Name);
            var classifier = new PoiClassifier(await _catalog.GetRulesAsync(cancellationToken));
            var results = new List<SourceImportResult>();

            foreach (var source in _options.PoiSources)
            {
                results.Add(await ImportSourceAsync(source, classifier, categoryNames, cancellationToken));
            }

            return new ImportReport(results);
        }

        public static string BuildExternalId(string rawId, PoiSourceOptions source)
        {
            var trimmed = rawId.Trim();

            if (source.OsmIdentifiers && trimmed.StartsWith('-'))
            {
                // osm2pgsql guarda las relaciones con id negativo
                return $"r{trimmed[1..]}";
            }

            return $"{source.IdPrefix}{trimmed}";
        }

        public static Ownership ResolveOwnership(PoiSourceOptions source, IReadOnlyDictionary<string, string> attributes)
        {
            if (source.OwnershipAttribute is null || !attributes.TryGetValue(source.OwnershipAttribute, out var value))
            {
                return Ownership.Unknown;
            }

            if (source.PublicValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return Ownership.Public;
            }

            return source.PrivateValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase)
                ? Ownership.Private
                : Ownership.Unknown;
        }

        private async Task<SourceImportResult> ImportSourceAsync(
            PoiSourceOptions source,
            PoiClassifier classifier,
            IReadOnlyDictionary<short, string> categoryNames,
            CancellationToken cancellationToken)
        {
            var dataSource = await _dataSources.GetByCodeAsync(source.DataSourceCode, cancellationToken)
                ?? throw new InvalidOperationException($"La fuente '{source.DataSourceCode}' no está registrada en data_sources.");

            var result = new SourceImportResult(source.DataSourceCode, source.Table);
            var batch = new List<PointOfInterest>(_options.BatchSize);

            await foreach (var feature in _reader.ReadAsync(source, cancellationToken))
            {
                result.Read++;

                var categoryId = classifier.Classify(dataSource.Id, feature.Attributes);

                if (categoryId is null)
                {
                    result.Skipped++;
                    continue;
                }

                PointOfInterest poi;

                try
                {
                    poi = PointOfInterest.Create(
                        categoryId.Value,
                        feature.Name,
                        categoryNames[categoryId.Value],
                        feature.Address,
                        feature.Geometry,
                        ResolveOwnership(source, feature.Attributes),
                        new Dictionary<string, string>(feature.Attributes),
                        dataSource.Id,
                        BuildExternalId(feature.RawId, source),
                        source.SourceDate,
                        _clock.GetUtcNow());
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
                {
                    result.Invalid++;
                    continue;
                }

                batch.Add(poi);

                if (batch.Count >= _options.BatchSize)
                {
                    await FlushAsync(dataSource.Id, batch, result, cancellationToken);
                }
            }

            if (batch.Count > 0)
            {
                await FlushAsync(dataSource.Id, batch, result, cancellationToken);
            }

            return result;
        }

        private async Task FlushAsync(
            short dataSourceId,
            List<PointOfInterest> batch,
            SourceImportResult result,
            CancellationToken cancellationToken)
        {
            await _pois.UpsertAsync(dataSourceId, batch.ToList(), cancellationToken);
            result.Imported += batch.Count;
            batch.Clear();
        }
    }
}
