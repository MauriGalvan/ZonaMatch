using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // SQL shared by the queries that work on a zone (ZoneRepository, PointOfInterestRepository), so they all pick
    // the same boundary for a slug.
    // Zones are the administrative boundaries of the osm2pgsql import (schema "osm", SRID 3857).
    // AMBA admin levels: 4 for CABA, 5 for communes and districts, 8 for localities, and 9 or 10 for neighborhoods.
    // The province of Buenos Aires is not there: the AMBA bounding box cuts it, so osm2pgsql cannot build it.
    internal static class ZoneSql
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;
        private const string Geo = ZonaMatchDbOptions.GeoSchema;

        // admin_level is free text in OSM; the CASE keeps a bad value from breaking the cast
        public static string GetAdministrativeLevel(string alias) => $"CASE WHEN {alias}.admin_level ~ '^[0-9]+$' THEN {alias}.admin_level::int END";

        // CTEs "partes" and "zona" (osm_id, name, nivel, way in SRID 3857, geom in SRID 4326, way_area) for @slug.
        // The slug is looked up in geo.zonas (etl/calcular_zonas.sql), where every zone has a unique slug. When places
        // share a name, the district is added ("villa-sarmiento-moron"). osm2pgsql stores each multipolygon part in
        // a separate row with the same osm_id, so the parts are joined back together.
        public static readonly string ZoneBySlug = $"""
            partes AS (
                SELECT t.osm_id, t.name, {GetAdministrativeLevel("t")} AS nivel, t.way, t.way_area
                FROM {Schema}.planet_osm_polygon t
                JOIN {Geo}.zonas g ON g.osm_id = t.osm_id AND g.elegida AND g.slug = @slug
                WHERE t.boundary = 'administrative'
            ),
            zona AS (
                SELECT osm_id, name, nivel, ST_Union(way) AS way, ST_Transform(ST_Union(way), 4326) AS geom,
                       sum(way_area) AS way_area
                FROM partes
                GROUP BY osm_id, name, nivel
                ORDER BY nivel DESC, sum(way_area) DESC
                LIMIT 1
            )
            """;

        // Center of a zone geometry in 4326: the centroid, or a point inside it when the centroid falls outside
        // (concave shapes)
        public static string GetCenter(string geom) =>
            $"CASE WHEN ST_Contains({geom}, ST_Centroid({geom})) THEN ST_Centroid({geom}) ELSE ST_PointOnSurface({geom}) END";
    }
}
