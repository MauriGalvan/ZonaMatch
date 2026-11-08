using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Zones are the administrative boundaries of the OSM import (see ZoneSql). PostGIS picks the zone and works out
    // what needs the geometry (jurisdictions, center, simplified boundary); Application builds the GeoJSON.
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
            SELECT zone.osm_id AS "OsmId",
                   @slug AS "Slug",
                   zone.name AS "Name",
                   zone.administrative_level AS "AdministrativeLevel",
                   round((ST_Area(zone.geom::geography) / 1e6)::numeric, 2)::float8 AS "AreaKm2",
                   COALESCE((SELECT array_agg(jurisdiction.name ORDER BY jurisdiction.administrative_level DESC, jurisdiction.name)
                             FROM jurisdictions jurisdiction), ARRAY[]::text[]) AS "JurisdictionNames",
                   COALESCE((SELECT array_agg(jurisdiction.administrative_level ORDER BY jurisdiction.administrative_level DESC, jurisdiction.name)
                             FROM jurisdictions jurisdiction), ARRAY[]::int[]) AS "JurisdictionAdministrativeLevels",
                   ST_Y(center.center) AS "CenterLatitude",
                   ST_X(center.center) AS "CenterLongitude",
                   ST_AsGeoJSON(ST_SimplifyPreserveTopology(zone.geom, @simplificationTolerance), {GeoJsonCoordinatePrecision}) AS "GeometryGeoJson"
            FROM selected_zone zone, zone_center center
            """;

        // Search the same selected zones as ZoneDetailsSql so each result opens its own zone. Places that share a name
        // appear separately with their own slug and jurisdictions. Jurisdictions are returned as parallel name and
        // administrative-level arrays.
        // The search text becomes a slug with geo.slug_zona, the function the ETL uses for the zone slugs, so both
        // follow one rule (it also leaves no LIKE wildcards: everything but a-z and 0-9 turns into "-").
        private static readonly string SearchZonesSql = $"""
            WITH search AS (
                SELECT {Geo}.slug_zona(@searchText) AS fragment
            ),
            zones AS (
                SELECT g.osm_id, g.slug, g.nombre AS name, g.nivel AS administrative_level,
                       sum(t.way_area) AS area,
                       sum(ST_Area(ST_Transform(t.way, 4326)::geography)) AS area_m2,
                       ST_PointOnSurface((array_agg(t.way ORDER BY t.way_area DESC))[1]) AS location
                FROM {Geo}.zonas g
                JOIN {Schema}.planet_osm_polygon t ON t.osm_id = g.osm_id AND t.boundary = 'administrative'
                CROSS JOIN search s
                WHERE g.elegida AND s.fragment <> '' AND g.slug LIKE '%' || s.fragment || '%'
                GROUP BY g.osm_id, g.slug, g.nombre, g.nivel
            ),
            selected_zones AS (
                SELECT zones.*, zones.slug LIKE s.fragment || '%' AS starts_with_search
                FROM zones CROSS JOIN search s
                ORDER BY starts_with_search DESC, length(zones.slug), zones.slug
                LIMIT @resultLimit
            )
            SELECT e.osm_id AS "OsmId",
                   e.slug AS "Slug",
                   e.name AS "Name",
                   e.administrative_level AS "AdministrativeLevel",
                   round((e.area_m2 / 1e6)::numeric, 2)::float8 AS "AreaKm2",
                   COALESCE(j.names, ARRAY[]::text[]) AS "JurisdictionNames",
                   COALESCE(j.administrative_levels, ARRAY[]::int[]) AS "JurisdictionAdministrativeLevels",
                   -- not needed to list results; present because ZoneRow is shared with ZoneDetailsSql
                   0::float8 AS "CenterLatitude",
                   0::float8 AS "CenterLongitude",
                   ''::text AS "GeometryGeoJson"
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
                SELECT array_agg(boundary.name ORDER BY boundary.administrative_level DESC, boundary.name) AS names,
                       array_agg(boundary.administrative_level ORDER BY boundary.administrative_level DESC, boundary.name) AS administrative_levels
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

        public async Task<Zone?> GetBySlugAsync(string slug, double simplificationTolerance, CancellationToken cancellationToken = default)
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<ZoneRow>(ZoneDetailsSql,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("simplificationTolerance", simplificationTolerance))
                .ToListAsync(cancellationToken);

            return rows.SingleOrDefault()?.ToZone(withBoundary: true);
        }

        public async Task<IReadOnlyList<Zone>> SearchAsync(
            string searchText, int resultLimit, CancellationToken cancellationToken = default)
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<ZoneRow>(SearchZonesSql,
                    new NpgsqlParameter("searchText", searchText),
                    new NpgsqlParameter("resultLimit", resultLimit))
                .ToListAsync(cancellationToken);

            return rows.Select(row => row.ToZone(withBoundary: false)).ToList();
        }

        // Keyless result type for ZoneDetailsSql and SearchZonesSql; property names match the SQL column aliases.
        // Search results have no center or geometry columns.
        private sealed class ZoneRow
        {
            public long OsmId { get; set; }
            public string Slug { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int AdministrativeLevel { get; set; }
            public double AreaKm2 { get; set; }
            public string[] JurisdictionNames { get; set; } = [];
            public int[] JurisdictionAdministrativeLevels { get; set; } = [];
            public double CenterLatitude { get; set; }
            public double CenterLongitude { get; set; }
            public string GeometryGeoJson { get; set; } = string.Empty;

            public Zone ToZone(bool withBoundary) => new()
            {
                OsmId = OsmId,
                Slug = Slug,
                Name = Name,
                AdministrativeLevel = AdministrativeLevel,
                AreaKm2 = AreaKm2,
                Jurisdictions = JurisdictionNames
                    .Zip(JurisdictionAdministrativeLevels, (name, level) => new ZoneJurisdiction(name, level))
                    .ToList(),
                Boundary = withBoundary ? new ZoneBoundary(CenterLatitude, CenterLongitude, GeometryGeoJson) : null
            };
        }
    }
}
