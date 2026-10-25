using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // SQL shared by the queries that work on a zone (ZonaRepository, PuntoInteresRepository), so they all pick
    // the same boundary for a slug.
    // Zones are the administrative boundaries of the osm2pgsql import (schema "osm", SRID 3857).
    // admin_level in the AMBA extract: 4 CABA, 5 comunas and partidos, 8 localidades, 9 and 10 barrios.
    // The province of Buenos Aires is not there: the AMBA bounding box cuts it, so osm2pgsql cannot build it.
    internal static class ZonaSql
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;
        private const string Geo = ZonaMatchDbOptions.GeoSchema;

        // admin_level is free text in OSM; the CASE keeps a bad value from breaking the cast
        public static string Nivel(string alias) => $"CASE WHEN {alias}.admin_level ~ '^[0-9]+$' THEN {alias}.admin_level::int END";

        // CTEs "partes" and "zona" (osm_id, name, nivel, way in 3857, geom in 4326, way_area) for the @slug parameter.
        // The slug is looked up in geo.zonas (etl/calcular_zonas.sql), where each zone has a unique one: places that
        // share a name get their partido added ("villa-sarmiento-moron"). osm2pgsql stores each part of a
        // multipolygon as its own row with the same osm_id: parts are joined back.
        public static readonly string ZonaPorSlug = $"""
            partes AS (
                SELECT t.osm_id, t.name, {Nivel("t")} AS nivel, t.way, t.way_area
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
        public static string Centro(string geom) =>
            $"CASE WHEN ST_Contains({geom}, ST_Centroid({geom})) THEN ST_Centroid({geom}) ELSE ST_PointOnSurface({geom}) END";
    }
}
