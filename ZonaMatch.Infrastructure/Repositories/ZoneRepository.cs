using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.DTOs.Zones.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Zones are the administrative boundaries of the OSM import (see ZoneSql).
    // The GeoJSON is built inside PostGIS, like MapRepository.
    public class ZoneRepository : IZoneRepository
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;
        private const string Geo = ZonaMatchDbOptions.GeoSchema;

        // Around one-meter precision keeps the payload small.
        private const int GeoJsonCoordinatePrecision = 5;

        private static string GetAdministrativeLevel(string alias) => ZoneSql.GetAdministrativeLevel(alias);

        // The zone is chosen as in ZoneSql.ZoneBySlug.
        // Jurisdictions: the boundaries that contain the zone, most specific first. Only ones at least as
        // large count, and one that covers the same area as a wider one is skipped (the city "Buenos Aires", level 8,
        // repeats CABA).
        private static readonly string SqlPorSlug = $"""
            WITH {ZoneSql.ZoneBySlug},
            zone_center AS (
                SELECT {ZoneSql.GetCenter("z.geom")} AS punto
                FROM zona z
            ),
            containing_boundaries AS (
                SELECT j.osm_id, j.name, {GetAdministrativeLevel("j")} AS administrative_level, j.way_area
                FROM {Schema}.planet_osm_polygon j, zona z
                WHERE j.boundary = 'administrative' AND j.name IS NOT NULL
                  AND {GetAdministrativeLevel("j")} BETWEEN 4 AND z.nivel - 1
                  AND ST_Contains(j.way, ST_PointOnSurface(z.way))
                  -- a smaller boundary can also hold that point (a comuna inside the city "Buenos Aires");
                  -- one of the same size counts (Commune 14 is exactly Palermo)
                  AND j.way_area >= 0.99 * z.way_area
            ),
            jurisdictions AS (
                SELECT j.name, j.administrative_level
                FROM containing_boundaries j
                WHERE NOT EXISTS (
                    SELECT 1 FROM containing_boundaries w
                    WHERE w.administrative_level < j.administrative_level
                      AND abs(w.way_area - j.way_area) < 0.01 * w.way_area)
            )
            SELECT json_build_object(
                'type', 'Feature',
                'id', CASE WHEN z.osm_id < 0 THEN 'r' || -z.osm_id ELSE 'w' || z.osm_id END,
                'properties', json_build_object(
                    'slug', @slug,
                    'name', z.name,
                    'administrativeLevel', z.nivel,
                    'jurisdictions', (
                        SELECT COALESCE(json_agg(json_build_object('name', j.name, 'administrativeLevel', j.administrative_level)
                                                 ORDER BY j.administrative_level DESC), '[]'::json)
                        FROM jurisdictions j),
                    'areaKm2', round((ST_Area(z.geom::geography) / 1e6)::numeric, 2),
                    'center', json_build_object('latitude', ST_Y(c.punto), 'longitude', ST_X(c.punto))),
                'geometry', ST_AsGeoJSON(ST_SimplifyPreserveTopology(z.geom, @simplificationTolerance), {GeoJsonCoordinatePrecision})::json)::text AS "Value"
            FROM zona z, zone_center c
            """;

        // Search the same selected zones as SqlPorSlug so each result opens its own zone. Places that share a name
        // appear separately with their own slug and jurisdictions. Jurisdictions are returned as parallel name and
        // administrative-level arrays.
        private static readonly string SqlBuscar = $"""
            WITH zonas AS (
                SELECT g.slug, g.nombre AS name, g.nivel,
                       sum(t.way_area) AS way_area,
                       sum(ST_Area(ST_Transform(t.way, 4326)::geography)) AS metros2,
                       ST_PointOnSurface((array_agg(t.way ORDER BY t.way_area DESC))[1]) AS punto
                FROM {Geo}.zonas g
                JOIN {Schema}.planet_osm_polygon t ON t.osm_id = g.osm_id AND t.boundary = 'administrative'
                WHERE g.elegida AND g.slug LIKE '%' || @searchFragment || '%'
                GROUP BY g.slug, g.nombre, g.nivel
            ),
            elegidas AS (
                SELECT *, slug LIKE @searchFragment || '%' AS starts_with_search
                FROM zonas
                ORDER BY starts_with_search DESC, length(slug), slug
                LIMIT @resultLimit
            )
            SELECT e.slug AS "Slug",
                   e.name AS "Name",
                   e.nivel AS "AdministrativeLevel",
                   round((e.metros2 / 1e6)::numeric, 2)::float8 AS "AreaKm2",
                   COALESCE(j.names, ARRAY[]::text[]) AS "JurisdictionNames",
                   COALESCE(j.administrative_levels, ARRAY[]::int[]) AS "JurisdictionAdministrativeLevels"
            FROM elegidas e
            LEFT JOIN LATERAL (
                WITH contenedores AS (
                    SELECT c.name, {GetAdministrativeLevel("c")} AS nivel, c.way_area
                    FROM {Schema}.planet_osm_polygon c
                    WHERE c.boundary = 'administrative' AND c.name IS NOT NULL
                      AND {GetAdministrativeLevel("c")} BETWEEN 4 AND e.nivel - 1
                      AND ST_Contains(c.way, e.punto)
                      AND c.way_area >= 0.99 * e.way_area
                )
                SELECT array_agg(c.name ORDER BY c.nivel DESC) AS names,
                       array_agg(c.nivel ORDER BY c.nivel DESC) AS administrative_levels
                FROM contenedores c
                WHERE NOT EXISTS (
                    SELECT 1 FROM contenedores w
                    WHERE w.nivel < c.nivel AND abs(w.way_area - c.way_area) < 0.01 * w.way_area)
            ) j ON true
            ORDER BY e.starts_with_search DESC, length(e.slug), e.slug
            """;

        private readonly ZonaMatchDbContext _dbContext;

        public ZoneRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static readonly string SqlExiste = $"""
            WITH {ZoneSql.ZoneBySlug}
            SELECT EXISTS (SELECT 1 FROM zona) AS "Value"
            """;

        private static readonly string SqlContienePunto = $"""
            WITH {ZoneSql.ZoneBySlug}
            SELECT EXISTS (
                SELECT 1 FROM zona z
                WHERE ST_Intersects(z.geom, ST_SetSRID(ST_MakePoint(@lon, @lat), 4326))) AS "Value"
            """;

        public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) =>
            _dbContext.Database
                .SqlQueryRaw<bool>(SqlExiste, new NpgsqlParameter("slug", slug))
                .SingleAsync(cancellationToken);

        public Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default) =>
            _dbContext.Database
                .SqlQueryRaw<bool>(SqlContienePunto,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("lat", latitude),
                    new NpgsqlParameter("lon", longitude))
                .SingleAsync(cancellationToken);

        public async Task<string?> GetBySlugAsync(string slug, double simplificationTolerance, CancellationToken cancellationToken = default)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<string>(SqlPorSlug,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("simplificationTolerance", simplificationTolerance))
                .ToListAsync(cancellationToken);

            return filas.SingleOrDefault();
        }

        public async Task<IReadOnlyList<ZoneSearchResultResponse>> SearchAsync(
            string searchFragment, int resultLimit, CancellationToken cancellationToken = default)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<ZoneSearchRow>(SqlBuscar,
                    new NpgsqlParameter("searchFragment", searchFragment),
                    new NpgsqlParameter("resultLimit", resultLimit))
                .ToListAsync(cancellationToken);

            return filas
                .Select(f => new ZoneSearchResultResponse(
                    f.Slug,
                    f.Name,
                    f.AdministrativeLevel,
                    f.JurisdictionNames.Zip(f.JurisdictionAdministrativeLevels,
                        (name, administrativeLevel) => new JurisdictionResponse(name, administrativeLevel)).ToList(),
                    f.AreaKm2))
                .ToList();
        }

        // Keyless result type for SqlBuscar; property names match the SQL column aliases.
        private sealed class ZoneSearchRow
        {
            public string Slug { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int AdministrativeLevel { get; set; }
            public double AreaKm2 { get; set; }
            public string[] JurisdictionNames { get; set; } = [];
            public int[] JurisdictionAdministrativeLevels { get; set; } = [];
        }
    }
}
