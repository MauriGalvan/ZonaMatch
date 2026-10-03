using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Geo;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class PoiCatalogRepository : IPoiCatalogRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public PoiCatalogRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<PoiCategory>> GetCategoriesAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.PoiCategories.AsNoTracking().OrderBy(category => category.Id).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PoiMappingRule>> GetRulesAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.PoiMappingRules.AsNoTracking().OrderBy(rule => rule.Id).ToListAsync(cancellationToken);
        }
    }

    public class PoiRepository : IPoiRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public PoiRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task UpsertAsync(short dataSourceId, IReadOnlyList<PointOfInterest> pois, CancellationToken cancellationToken)
        {
            var externalIds = pois.Select(poi => poi.ExternalId).ToList();
            var existing = await _dbContext.PointsOfInterest
                .Where(poi => poi.DataSourceId == dataSourceId && externalIds.Contains(poi.ExternalId))
                .ToDictionaryAsync(poi => poi.ExternalId, StringComparer.Ordinal, cancellationToken);

            foreach (var poi in pois)
            {
                if (existing.TryGetValue(poi.ExternalId, out var current))
                {
                    current.UpdateFrom(poi);
                }
                else
                {
                    _dbContext.PointsOfInterest.Add(poi);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }

        public async Task<IReadOnlyList<PoiData>> FindWithinAsync(
            Point center,
            double radiusMeters,
            IReadOnlyCollection<string> rootCategoryCodes,
            int limit,
            CancellationToken cancellationToken)
        {
            var filterByLayer = rootCategoryCodes.Count > 0;

            var rows = await (
                    from poi in NearbyCanonical(center, radiusMeters)
                    join category in _dbContext.PoiCategories on poi.CategoryId equals category.Id
                    join root in _dbContext.PoiCategories on category.ParentId equals root.Id
                    join source in _dbContext.DataSources on poi.DataSourceId equals source.Id
                    where !filterByLayer || rootCategoryCodes.Contains(root.Code)
                    let distance = EF.Functions.Distance(poi.Location, center, true)
                    orderby distance, poi.Id
                    select new
                    {
                        poi.Id,
                        poi.Name,
                        poi.Address,
                        CategoryCode = category.Code,
                        CategoryName = category.Name,
                        RootCode = root.Code,
                        poi.Ownership,
                        poi.Location,
                        SourceName = source.Name,
                        poi.SourceDate,
                        Distance = distance,
                    })
                .Take(limit)
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new PoiData(
                    row.Id,
                    row.Name,
                    row.Address,
                    row.CategoryCode,
                    row.CategoryName,
                    row.RootCode,
                    row.Ownership,
                    row.Location,
                    row.SourceName,
                    row.SourceDate,
                    row.Distance))
                .ToList();
        }

        public async Task<IReadOnlyList<PoiCategoryCount>> CountWithinAsync(Point center, double radiusMeters, CancellationToken cancellationToken)
        {
            return await (
                    from poi in NearbyCanonical(center, radiusMeters)
                    join category in _dbContext.PoiCategories on poi.CategoryId equals category.Id
                    join root in _dbContext.PoiCategories on category.ParentId equals root.Id
                    group poi by new { RootCode = root.Code, category.Code } into grouped
                    select new PoiCategoryCount(grouped.Key.RootCode, grouped.Key.Code, grouped.Count()))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PoiDuplicateCandidate>> FindDuplicateCandidatesAsync(
            double maxDistanceMeters,
            CancellationToken cancellationToken)
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<DuplicateCandidateRow>(
                    $$"""
                    SELECT a.id AS first_id, a.normalized_name AS first_name, ST_X(a.location) AS first_x, ST_Y(a.location) AS first_y,
                           sa.kind AS first_kind,
                           b.id AS second_id, b.normalized_name AS second_name, ST_X(b.location) AS second_x, ST_Y(b.location) AS second_y,
                           sb.kind AS second_kind,
                           ST_Distance(a.location::geography, b.location::geography) AS distance
                    FROM {{ZonaMatchDbContext.Schema}}.points_of_interest a
                    JOIN {{ZonaMatchDbContext.Schema}}.poi_categories ca ON ca.id = a.category_id
                    JOIN {{ZonaMatchDbContext.Schema}}.data_sources sa ON sa.id = a.data_source_id
                    JOIN {{ZonaMatchDbContext.Schema}}.points_of_interest b
                      ON b.id > a.id
                     AND b.data_source_id <> a.data_source_id
                     AND ST_DWithin(a.location::geography, b.location::geography, {0})
                    JOIN {{ZonaMatchDbContext.Schema}}.poi_categories cb ON cb.id = b.category_id AND cb.parent_id = ca.parent_id
                    JOIN {{ZonaMatchDbContext.Schema}}.data_sources sb ON sb.id = b.data_source_id
                    WHERE a.canonical_poi_id IS NULL AND b.canonical_poi_id IS NULL
                    """,
                    maxDistanceMeters)
                .ToListAsync(cancellationToken);

            return rows.Select(row => new PoiDuplicateCandidate(
                    new PoiMatchCandidate(row.first_id, row.first_name, ToPoint(row.first_x, row.first_y), (DataSourceKind)row.first_kind),
                    new PoiMatchCandidate(row.second_id, row.second_name, ToPoint(row.second_x, row.second_y), (DataSourceKind)row.second_kind),
                    row.distance))
                .ToList();
        }

        public async Task MarkDuplicatesAsync(IReadOnlyList<PoiDuplicate> duplicates, CancellationToken cancellationToken)
        {
            var ids = duplicates.Select(duplicate => duplicate.DuplicateId).ToList();
            var pois = await _dbContext.PointsOfInterest
                .Where(poi => ids.Contains(poi.Id))
                .ToDictionaryAsync(poi => poi.Id, cancellationToken);

            foreach (var duplicate in duplicates)
            {
                pois[duplicate.DuplicateId].MarkAsDuplicateOf(duplicate.CanonicalId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }

        private IQueryable<PointOfInterest> NearbyCanonical(Point center, double radiusMeters)
        {
            // distancia en metros sobre el esferoide (geography), no en grados
            return _dbContext.PointsOfInterest
                .AsNoTracking()
                .Where(poi => poi.CanonicalPoiId == null && EF.Functions.IsWithinDistance(poi.Location, center, radiusMeters, true));
        }

        private static Point ToPoint(double longitude, double latitude) =>
            GeometryNormalizer.Factory.CreatePoint(new Coordinate(longitude, latitude));

        // columnas tal como las devuelve el SQL de candidatos
#pragma warning disable IDE1006
        private sealed class DuplicateCandidateRow
        {
            public long first_id { get; set; }
            public string first_name { get; set; } = string.Empty;
            public double first_x { get; set; }
            public double first_y { get; set; }
            public short first_kind { get; set; }
            public long second_id { get; set; }
            public string second_name { get; set; } = string.Empty;
            public double second_x { get; set; }
            public double second_y { get; set; }
            public short second_kind { get; set; }
            public double distance { get; set; }
        }
#pragma warning restore IDE1006
    }
}
