using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Domain.Territory;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class DataSourceRepository : IDataSourceRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public DataSourceRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<DataSource?> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            return await _dbContext.DataSources
                .AsNoTracking()
                .FirstOrDefaultAsync(source => source.Code == code, cancellationToken);
        }
    }

    public class TerritorialUnitRepository : ITerritorialUnitRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public TerritorialUnitRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyDictionary<string, long>> GetCodeIndexAsync(TerritorialUnitType type, CancellationToken cancellationToken)
        {
            return await _dbContext.TerritorialUnits
                .Where(unit => unit.Type == type && unit.Code != null)
                .ToDictionaryAsync(unit => unit.Code!, unit => unit.Id, StringComparer.Ordinal, cancellationToken);
        }

        public async Task<TerritorialUnitSummary?> FindContainingAsync(
            IReadOnlyCollection<TerritorialUnitType> types,
            Point point,
            CancellationToken cancellationToken)
        {
            // si hay unidades superpuestas de distintas fuentes, la más chica es la más específica
            return await _dbContext.TerritorialUnits
                .Where(unit => types.Contains(unit.Type) && unit.Geometry.Contains(point))
                .OrderBy(unit => unit.AreaM2)
                .Select(unit => new TerritorialUnitSummary(unit.Id, unit.Type, unit.Code, unit.Name, unit.ParentId))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TerritorialUnitSummary>> GetByTypesAsync(
            IReadOnlyCollection<TerritorialUnitType> types,
            CancellationToken cancellationToken)
        {
            return await _dbContext.TerritorialUnits
                .Where(unit => types.Contains(unit.Type))
                .OrderBy(unit => unit.Id)
                .Select(unit => new TerritorialUnitSummary(unit.Id, unit.Type, unit.Code, unit.Name, unit.ParentId))
                .ToListAsync(cancellationToken);
        }

        public async Task UpsertAsync(short dataSourceId, IReadOnlyList<TerritorialUnit> units, CancellationToken cancellationToken)
        {
            var externalIds = units.Select(unit => unit.ExternalId).ToList();
            var existing = await _dbContext.TerritorialUnits
                .Where(unit => unit.DataSourceId == dataSourceId && externalIds.Contains(unit.ExternalId))
                .ToDictionaryAsync(unit => unit.ExternalId, StringComparer.Ordinal, cancellationToken);

            foreach (var unit in units)
            {
                if (existing.TryGetValue(unit.ExternalId, out var current))
                {
                    current.UpdateFrom(unit);
                }
                else
                {
                    _dbContext.TerritorialUnits.Add(unit);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }
    }

    public class ZoneRepository : IZoneRepository
    {
        // ~50 m en grados: los bordes de fuentes distintas no siempre se tocan exactamente
        private const double AdjacencyToleranceDegrees = 0.0005d;

        private readonly ZonaMatchDbContext _dbContext;

        public ZoneRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SyncAsync(IReadOnlyList<Zone> zones, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.Zones.ToDictionaryAsync(zone => zone.TerritorialUnitId, cancellationToken);
            var incoming = zones.Select(zone => zone.TerritorialUnitId).ToHashSet();

            _dbContext.Zones.RemoveRange(existing.Values.Where(zone => !incoming.Contains(zone.TerritorialUnitId)));

            foreach (var zone in zones)
            {
                if (existing.TryGetValue(zone.TerritorialUnitId, out var current))
                {
                    current.UpdateFrom(zone);
                }
                else
                {
                    _dbContext.Zones.Add(zone);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }

        public async Task<int> RebuildAdjacenciesAsync(CancellationToken cancellationToken)
        {
            await _dbContext.ZoneAdjacencies.ExecuteDeleteAsync(cancellationToken);

            return await _dbContext.Database.ExecuteSqlRawAsync(
                $$"""
                INSERT INTO {{ZonaMatchDbContext.Schema}}.zone_adjacencies (zone_id, neighbor_zone_id)
                SELECT a.id, b.id
                FROM {{ZonaMatchDbContext.Schema}}.zones a
                JOIN {{ZonaMatchDbContext.Schema}}.territorial_units ua ON ua.id = a.territorial_unit_id
                JOIN {{ZonaMatchDbContext.Schema}}.zones b ON b.id <> a.id
                JOIN {{ZonaMatchDbContext.Schema}}.territorial_units ub ON ub.id = b.territorial_unit_id
                WHERE ST_DWithin(ua.geometry, ub.geometry, {0})
                """,
                [AdjacencyToleranceDegrees],
                cancellationToken);
        }

        public async Task<IReadOnlyList<ZoneSummary>> SearchAsync(string normalizedQuery, int limit, CancellationToken cancellationToken)
        {
            // el slug ya está normalizado e incluye el partido: "ramos-mejia-la-matanza"
            var slugQuery = normalizedQuery.ToLowerInvariant().Replace(' ', '-');

            return await _dbContext.Zones
                .Where(zone => zone.Slug.Contains(slugQuery))
                .OrderBy(zone => zone.Name)
                .ThenBy(zone => zone.ParentName)
                .Take(limit)
                .Select(zone => new ZoneSummary(zone.Id, zone.Slug, zone.Name, zone.ParentName, zone.TerritorialUnit!.Type))
                .ToListAsync(cancellationToken);
        }

        public async Task<ZoneData?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
        {
            var zones = await GetBySlugsAsync([slug], cancellationToken);
            return zones.SingleOrDefault();
        }

        public async Task<IReadOnlyList<ZoneData>> GetBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken)
        {
            var rows = await _dbContext.Zones
                .Where(zone => slugs.Contains(zone.Slug))
                .Select(zone => new
                {
                    zone.Id,
                    zone.Slug,
                    zone.Name,
                    zone.ParentName,
                    zone.TerritorialUnit!.Type,
                    zone.TerritorialUnitId,
                    zone.TerritorialUnit.ParentId,
                    ParentUnitName = zone.TerritorialUnit.Parent!.Name,
                    zone.TerritorialUnit.AreaM2,
                    Center = zone.TerritorialUnit.Geometry.InteriorPoint,
                    zone.TerritorialUnit.Geometry,
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new ZoneData(
                    row.Id,
                    row.Slug,
                    row.Name,
                    row.ParentName,
                    row.Type,
                    row.TerritorialUnitId,
                    row.ParentId,
                    row.ParentUnitName,
                    row.AreaM2,
                    (Point)row.Center,
                    row.Geometry))
                .ToList();
        }

        public async Task<ZoneSummary?> GetByTerritorialUnitIdAsync(long territorialUnitId, CancellationToken cancellationToken)
        {
            return await _dbContext.Zones
                .Where(zone => zone.TerritorialUnitId == territorialUnitId)
                .Select(zone => new ZoneSummary(zone.Id, zone.Slug, zone.Name, zone.ParentName, zone.TerritorialUnit!.Type))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ZoneSummary>> GetNeighborsAsync(int zoneId, CancellationToken cancellationToken)
        {
            return await (
                    from adjacency in _dbContext.ZoneAdjacencies
                    join zone in _dbContext.Zones on adjacency.NeighborZoneId equals zone.Id
                    where adjacency.ZoneId == zoneId
                    orderby zone.Name
                    select new ZoneSummary(zone.Id, zone.Slug, zone.Name, zone.ParentName, zone.TerritorialUnit!.Type))
                .ToListAsync(cancellationToken);
        }
    }

    public class IndicatorRepository : IIndicatorRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public IndicatorRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<IndicatorData>> GetAsync(
            IReadOnlyCollection<long> territorialUnitIds,
            IReadOnlyCollection<string> indicatorCodes,
            CancellationToken cancellationToken)
        {
            return await (
                    from indicator in _dbContext.TerritorialIndicators
                    join source in _dbContext.DataSources on indicator.DataSourceId equals source.Id
                    where territorialUnitIds.Contains(indicator.TerritorialUnitId) && indicatorCodes.Contains(indicator.IndicatorCode)
                    select new IndicatorData(
                        indicator.TerritorialUnitId,
                        indicator.IndicatorCode,
                        indicator.Value,
                        indicator.Unit,
                        source.Name,
                        indicator.ReferenceDate))
                .ToListAsync(cancellationToken);
        }
    }
}
