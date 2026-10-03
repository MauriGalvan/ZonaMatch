using System.Runtime.CompilerServices;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using ZonaMatch.Application.Ingestion;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Sources
{
    // lee tablas de origen que viven en la misma base (zm.*, public.planet_osm_*).
    // usa una conexión propia para no bloquear al DbContext que va guardando los lotes
    public class PostgisTerritorialSourceReader : ITerritorialSourceReader
    {
        private readonly NpgsqlDataSource _dataSource;

        public PostgisTerritorialSourceReader(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public static string BuildQuery(TerritorialSourceOptions source)
        {
            var id = SqlIdentifier.Quote(source.IdColumn);
            var geometry = SqlIdentifier.Quote(source.GeometryColumn);
            var order = source.PreferredRowCondition is null
                ? $"ST_Area(t.{geometry}) DESC"
                : $"({source.PreferredRowCondition}) DESC, ST_Area(t.{geometry}) DESC";

            // varias filas con el mismo id se reagrupan: osm2pgsql parte los multipolígonos y
            // zm.partidos guarda las islas del delta aparte con el mismo código censal.
            // los atributos se toman de la fila preferida o, si no hay preferencia, de la más grande
            return $"""
                SELECT CAST(t.{id} AS text) AS raw_id,
                       {First(source.NameColumn, order)} AS name,
                       {First(source.CodeColumn, order)} AS code,
                       {First(source.ParentCodeColumn, order)} AS parent_code,
                       ST_AsBinary(ST_Collect(t.{geometry})) AS wkb,
                       ST_SRID(ST_Collect(t.{geometry})) AS srid
                FROM {SqlIdentifier.Quote(source.Table)} t
                WHERE t.{geometry} IS NOT NULL AND ({source.Filter ?? "TRUE"})
                GROUP BY t.{id}
                ORDER BY 1
                """;
        }

        public async IAsyncEnumerable<RawTerritorialFeature> ReadAsync(
            TerritorialSourceOptions source,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var command = _dataSource.CreateCommand(BuildQuery(source));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var wkbReader = new WKBReader();

            while (await reader.ReadAsync(cancellationToken))
            {
                yield return new RawTerritorialFeature(
                    reader.GetString(0),
                    ReaderValues.Text(reader, 1),
                    ReaderValues.Text(reader, 2),
                    ReaderValues.Text(reader, 3),
                    ReaderValues.Geometry(wkbReader, reader, 4, 5));
            }
        }

        private static string First(string? column, string order) =>
            column is null
                ? "NULL::text"
                : $"(array_agg(CAST(t.{SqlIdentifier.Quote(column)} AS text) ORDER BY {order}))[1]";
    }

    public class PostgisPoiSourceReader : IPoiSourceReader
    {
        private readonly NpgsqlDataSource _dataSource;

        public PostgisPoiSourceReader(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public static string BuildQuery(PoiSourceOptions source)
        {
            var id = SqlIdentifier.Quote(source.IdColumn);
            var geometry = SqlIdentifier.Quote(source.GeometryColumn);
            var attributes = source.AttributeColumns
                .Select((column, index) => $"(array_agg(CAST(t.{SqlIdentifier.Quote(column)} AS text)))[1] AS a{index}");
            var tags = source.TagsColumn is null
                ? "NULL::text"
                : $"(array_agg(hstore_to_jsonb(t.{SqlIdentifier.Quote(source.TagsColumn)})))[1]::text";

            return $"""
                SELECT CAST(t.{id} AS text) AS raw_id,
                       (array_agg(CAST(t.{SqlIdentifier.Quote(source.NameColumn)} AS text)))[1] AS name,
                       ST_AsBinary(ST_Collect(t.{geometry})) AS wkb,
                       ST_SRID(ST_Collect(t.{geometry})) AS srid,
                       {tags} AS tags{string.Concat(attributes.Select(attribute => $",\n       {attribute}"))}
                FROM {SqlIdentifier.Quote(source.Table)} t
                WHERE t.{geometry} IS NOT NULL AND ({source.Filter ?? "TRUE"})
                GROUP BY t.{id}
                ORDER BY 1
                """;
        }

        // columnas propias primero; las etiquetas hstore completan lo que falte
        public static Dictionary<string, string> MergeAttributes(
            IReadOnlyList<string> columns,
            IReadOnlyList<string?> values,
            string? tagsJson)
        {
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var index = 0; index < columns.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    attributes[columns[index]] = values[index]!.Trim();
                }
            }

            if (tagsJson is not null)
            {
                foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string?>>(tagsJson)!)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        attributes.TryAdd(key, value.Trim());
                    }
                }
            }

            return attributes;
        }

        public static string? BuildAddress(IReadOnlyList<string> addressColumns, IReadOnlyDictionary<string, string> attributes)
        {
            var parts = addressColumns
                .Select(column => attributes.GetValueOrDefault(column))
                .OfType<string>()
                .ToList();

            return parts.Count == 0 ? null : string.Join(' ', parts);
        }

        public async IAsyncEnumerable<RawPoiFeature> ReadAsync(
            PoiSourceOptions source,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var command = _dataSource.CreateCommand(BuildQuery(source));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var wkbReader = new WKBReader();

            while (await reader.ReadAsync(cancellationToken))
            {
                var values = source.AttributeColumns
                    .Select((_, index) => ReaderValues.Text(reader, 5 + index))
                    .ToList();
                var attributes = MergeAttributes(source.AttributeColumns, values, ReaderValues.Text(reader, 4));

                yield return new RawPoiFeature(
                    reader.GetString(0),
                    ReaderValues.Text(reader, 1),
                    BuildAddress(source.AddressColumns, attributes),
                    attributes,
                    ReaderValues.Geometry(wkbReader, reader, 2, 3));
            }
        }
    }

    internal static class ReaderValues
    {
        public static string? Text(NpgsqlDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        public static Geometry Geometry(WKBReader wkbReader, NpgsqlDataReader reader, int wkbOrdinal, int sridOrdinal)
        {
            var geometry = wkbReader.Read(reader.GetFieldValue<byte[]>(wkbOrdinal));
            geometry.SRID = reader.GetInt32(sridOrdinal);
            return geometry;
        }
    }
}
