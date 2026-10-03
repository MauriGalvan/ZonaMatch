using Microsoft.Extensions.Options;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Application.Ingestion
{
    // lleva las unidades territoriales de cada fuente configurada al modelo canónico
    public class TerritorialImportService
    {
        private readonly ITerritorialSourceReader _reader;
        private readonly ITerritorialUnitRepository _units;
        private readonly IDataSourceRepository _dataSources;
        private readonly TimeProvider _clock;
        private readonly IngestionOptions _options;

        public TerritorialImportService(
            ITerritorialSourceReader reader,
            ITerritorialUnitRepository units,
            IDataSourceRepository dataSources,
            TimeProvider clock,
            IOptions<IngestionOptions> options)
        {
            _reader = reader;
            _units = units;
            _dataSources = dataSources;
            _clock = clock;
            _options = options.Value;
        }

        public async Task<ImportReport> ImportAsync(CancellationToken cancellationToken)
        {
            var results = new List<SourceImportResult>();

            // el orden importa: los padres (comunas, partidos) se cargan antes que los hijos
            foreach (var source in _options.TerritorialSources)
            {
                results.Add(await ImportSourceAsync(source, cancellationToken));
            }

            return new ImportReport(results);
        }

        private async Task<SourceImportResult> ImportSourceAsync(TerritorialSourceOptions source, CancellationToken cancellationToken)
        {
            var dataSource = await _dataSources.GetByCodeAsync(source.DataSourceCode, cancellationToken)
                ?? throw new InvalidOperationException($"La fuente '{source.DataSourceCode}' no está registrada en data_sources.");

            var parentIndex = source.ParentType is not null && source.ParentCodeColumn is not null
                ? await _units.GetCodeIndexAsync(source.ParentType.Value, cancellationToken)
                : null;

            var result = new SourceImportResult(source.DataSourceCode, source.Type.ToString());
            var batch = new List<TerritorialUnit>(_options.BatchSize);

            await foreach (var feature in _reader.ReadAsync(source, cancellationToken))
            {
                result.Read++;

                TerritorialUnit unit;

                try
                {
                    unit = TerritorialUnit.Create(
                        source.Type,
                        feature.Code,
                        feature.Name ?? string.Empty,
                        feature.Geometry,
                        dataSource.Id,
                        feature.RawId,
                        source.SourceDate,
                        _clock.GetUtcNow());
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
                {
                    result.Invalid++;
                    continue;
                }

                if (source.ParentType is not null)
                {
                    var parentId = await ResolveParentAsync(source.ParentType.Value, feature, unit, parentIndex, cancellationToken);

                    if (parentId is null)
                    {
                        result.Skipped++;
                        continue;
                    }

                    unit.AssignParent(parentId.Value);
                }

                batch.Add(unit);

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

        private async Task<long?> ResolveParentAsync(
            TerritorialUnitType parentType,
            RawTerritorialFeature feature,
            TerritorialUnit unit,
            IReadOnlyDictionary<string, long>? parentIndex,
            CancellationToken cancellationToken)
        {
            if (parentIndex is not null)
            {
                return feature.ParentCode is not null && parentIndex.TryGetValue(feature.ParentCode.Trim(), out var parentId)
                    ? parentId
                    : null;
            }

            var interiorPoint = (NetTopologySuite.Geometries.Point)unit.Geometry.InteriorPoint;
            var parent = await _units.FindContainingAsync([parentType], interiorPoint, cancellationToken);
            return parent?.Id;
        }

        private async Task FlushAsync(
            short dataSourceId,
            List<TerritorialUnit> batch,
            SourceImportResult result,
            CancellationToken cancellationToken)
        {
            await _units.UpsertAsync(dataSourceId, batch.ToList(), cancellationToken);
            result.Imported += batch.Count;
            batch.Clear();
        }
    }
}
