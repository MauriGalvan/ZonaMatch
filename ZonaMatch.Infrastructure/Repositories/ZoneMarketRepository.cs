using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // app.zone_market through EF; the comuna or partido of a zone from geo.zonas (etl/calcular_zonas.sql).
    public class ZoneMarketRepository(ZonaMatchDbContext dbContext) : IZoneMarketRepository
    {
        private const string Geo = ZonaMatchDbOptions.GeoSchema;

        private static readonly string SqlParent = $"""
            SELECT p.slug AS "Slug", p.nombre AS "Name"
            FROM {Geo}.zonas z
            JOIN {Geo}.zonas p ON p.elegida AND p.nivel = 5 AND p.osm_id <> z.osm_id
                              AND ST_Contains(p.geom, ST_PointOnSurface(z.geom))
            WHERE z.elegida AND z.slug = @slug AND z.nivel > 5
            ORDER BY p.superficie_km2
            LIMIT 1
            """;

        private static readonly string SqlName = $"""
            SELECT z.nombre AS "Value" FROM {Geo}.zonas z WHERE z.elegida AND z.slug = @slug LIMIT 1
            """;

        public async Task<IReadOnlyList<ZoneMarket>> ListByZoneAsync(string slug, CancellationToken cancellationToken = default) =>
            await dbContext.ZoneMarkets.AsNoTracking().Where(row => row.ZoneSlug == slug).ToListAsync(cancellationToken);

        public async Task<(string Slug, string Name)?> GetParentAsync(string slug, CancellationToken cancellationToken = default)
        {
            var parent = await dbContext.Database
                .SqlQueryRaw<ParentRow>(SqlParent, new NpgsqlParameter("slug", slug))
                .FirstOrDefaultAsync(cancellationToken);
            return parent is null ? null : (parent.Slug, parent.Name);
        }

        public async Task<string?> GetZoneNameAsync(string slug, CancellationToken cancellationToken = default) =>
            await dbContext.Database
                .SqlQueryRaw<string>(SqlName, new NpgsqlParameter("slug", slug))
                .FirstOrDefaultAsync(cancellationToken);

        private sealed class ParentRow
        {
            public string Slug { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}
