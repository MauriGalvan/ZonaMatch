using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.DTOs.Zone.Responses;
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
        private static readonly string ZoneDetailsSql = $"""
            WITH {ZoneSql.ZoneBySlug},
            zone_center AS (
                SELECT {ZoneSql.GetCenter("zone.geom")} AS center
                FROM selected_zone zone
            ),
            containing_boundaries AS (
                SELECT boundary.osm_id, boundary.name,
                       {GetAdministrativeLevel("boundary")} AS administrative_level, boundary.way_area
                FROM {Schema}.planet_osm_polygon boundary, selected_zone zone
                WHERE boundary.boundary = 'administrative' AND boundary.name IS NOT NULL
                  AND {GetAdministrativeLevel("boundary")} BETWEEN 4 AND zone.administrative_level - 1
                  AND ST_Contains(boundary.way, ST_PointOnSurface(zone.way))
                  -- a smaller boundary can also hold that point (a comuna inside the city "Buenos Aires");
                  -- one of the same size counts (Commune 14 is exactly Palermo)
                  AND boundary.way_area >= 0.99 * zone.area
            ),
            jurisdictions AS (
                SELECT boundary.name, boundary.administrative_level
                FROM containing_boundaries boundary
                WHERE NOT EXISTS (
                    SELECT 1 FROM containing_boundaries wider_boundary
                    WHERE wider_boundary.administrative_level < boundary.administrative_level
                      AND abs(wider_boundary.way_area - boundary.way_area) < 0.01 * wider_boundary.way_area)
            )
            SELECT json_build_object(
                'type', 'Feature',
                'id', CASE WHEN zone.osm_id < 0 THEN 'r' || -zone.osm_id ELSE 'w' || zone.osm_id END,
                'properties', json_build_object(
                    'slug', @slug,
                    'name', zone.name,
                    'administrativeLevel', zone.administrative_level,
                    'jurisdictions', (
                        SELECT COALESCE(json_agg(json_build_object('name', jurisdiction.name, 'administrativeLevel', jurisdiction.administrative_level)
                                                 ORDER BY jurisdiction.administrative_level DESC), '[]'::json)
                        FROM jurisdictions jurisdiction),
                    'areaKm2', round((ST_Area(zone.geom::geography) / 1e6)::numeric, 2),
                    'center', json_build_object('latitude', ST_Y(center.center), 'longitude', ST_X(center.center))),
                'geometry', ST_AsGeoJSON(ST_SimplifyPreserveTopology(zone.geom, @simplificationTolerance), {GeoJsonCoordinatePrecision})::json)::text AS "Value"
            FROM selected_zone zone, zone_center center
            """;

        // Search the same selected zones as ZoneDetailsSql so each result opens its own zone. Places that share a name
        // appear separately with their own slug and jurisdictions. Jurisdictions are returned as parallel name and
        // administrative-level arrays.
        private static readonly string SearchZonesSql = $"""
            WITH zones AS (
                SELECT g.slug, g.nombre AS name, g.nivel AS administrative_level,
                       sum(t.way_area) AS area,
                       sum(ST_Area(ST_Transform(t.way, 4326)::geography)) AS area_m2,
                       ST_PointOnSurface((array_agg(t.way ORDER BY t.way_area DESC))[1]) AS location
                FROM {Geo}.zonas g
                JOIN {Schema}.planet_osm_polygon t ON t.osm_id = g.osm_id AND t.boundary = 'administrative'
                WHERE g.elegida AND g.slug LIKE '%' || @searchFragment || '%'
                GROUP BY g.slug, g.nombre, g.nivel
            ),
            selected_zones AS (
                SELECT *, slug LIKE @searchFragment || '%' AS starts_with_search
                FROM zones
                ORDER BY starts_with_search DESC, length(slug), slug
                LIMIT @resultLimit
            )
            SELECT e.slug AS "Slug",
                   e.name AS "Name",
                   e.administrative_level AS "AdministrativeLevel",
                   round((e.area_m2 / 1e6)::numeric, 2)::float8 AS "AreaKm2",
                   COALESCE(j.names, ARRAY[]::text[]) AS "JurisdictionNames",
                   COALESCE(j.administrative_levels, ARRAY[]::int[]) AS "JurisdictionAdministrativeLevels"
            FROM selected_zones e
            LEFT JOIN LATERAL (
                WITH containing_boundaries AS (
                    SELECT boundary.name, {GetAdministrativeLevel("boundary")} AS administrative_level, boundary.way_area
                    FROM {Schema}.planet_osm_polygon boundary
                    WHERE boundary.boundary = 'administrative' AND boundary.name IS NOT NULL
                      AND {GetAdministrativeLevel("boundary")} BETWEEN 4 AND e.administrative_level - 1
                      AND ST_Contains(boundary.way, e.location)
                      AND boundary.way_area >= 0.99 * e.area
                )
                SELECT array_agg(boundary.name ORDER BY boundary.administrative_level DESC) AS names,
                       array_agg(boundary.administrative_level ORDER BY boundary.administrative_level DESC) AS administrative_levels
                FROM containing_boundaries boundary
                WHERE NOT EXISTS (
                    SELECT 1 FROM containing_boundaries wider_boundary
                    WHERE wider_boundary.administrative_level < boundary.administrative_level
                      AND abs(wider_boundary.way_area - boundary.way_area) < 0.01 * wider_boundary.way_area)
            ) j ON true
            ORDER BY e.starts_with_search DESC, length(e.slug), e.slug
            """;

        private readonly ZonaMatchDbContext _dbContext;

        public ZoneRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static readonly string ExistsSql = $"""
            WITH {ZoneSql.ZoneBySlug}
            SELECT EXISTS (SELECT 1 FROM selected_zone) AS "Value"
            """;

        private static readonly string ContainsPointSql = $"""
            WITH {ZoneSql.ZoneBySlug}
            SELECT EXISTS (
                SELECT 1 FROM selected_zone z
                WHERE ST_Intersects(z.geom, ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326))) AS "Value"
            """;

        public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) =>
            _dbContext.Database
                .SqlQueryRaw<bool>(ExistsSql, new NpgsqlParameter("slug", slug))
                .SingleAsync(cancellationToken);

        public Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default) =>
            _dbContext.Database
                .SqlQueryRaw<bool>(ContainsPointSql,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("latitude", latitude),
                    new NpgsqlParameter("longitude", longitude))
                .SingleAsync(cancellationToken);

        public async Task<string?> GetBySlugAsync(string slug, double simplificationTolerance, CancellationToken cancellationToken = default)
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<string>(ZoneDetailsSql,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("simplificationTolerance", simplificationTolerance))
                .ToListAsync(cancellationToken);

            return rows.SingleOrDefault();
        }

        public async Task<IReadOnlyList<ZoneSearchResultResponse>> SearchAsync(
            string searchFragment, int resultLimit, CancellationToken cancellationToken = default)
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<ZoneSearchRow>(SearchZonesSql,
                    new NpgsqlParameter("searchFragment", searchFragment),
                    new NpgsqlParameter("resultLimit", resultLimit))
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new ZoneSearchResultResponse(
                    row.Slug,
                    row.Name,
                    row.AdministrativeLevel,
                    row.JurisdictionNames.Zip(row.JurisdictionAdministrativeLevels,
                        (name, administrativeLevel) => new JurisdictionResponse(name, administrativeLevel)).ToList(),
                    row.AreaKm2))
                .ToList();
        }

        // Keyless result type for SearchZonesSql; property names match the SQL column aliases.
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
