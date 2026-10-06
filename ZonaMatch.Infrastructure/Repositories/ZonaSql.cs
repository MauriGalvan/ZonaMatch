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

        // Same rule as SlugZona.Generar: lowercase, no accents, non-alphanumeric runs -> "-"
        public const string Slug = """
            trim(both '-' from regexp_replace(
                translate(lower(t.name), 'áàäâãéèëêíìïîóòöôõúùüûñç', 'aaaaaeeeeiiiiooooouuuunc'),
                '[^a-z0-9]+', '-', 'g'))
            """;

        // admin_level is free text in OSM; the CASE keeps a bad value from breaking the cast
        public static string Nivel(string alias) => $"CASE WHEN {alias}.admin_level ~ '^[0-9]+$' THEN {alias}.admin_level::int END";

        // CTEs "partes" and "zona" (osm_id, name, nivel, way in 3857, geom in 4326, way_area) for the @slug parameter.
        // osm2pgsql stores each part of a multipolygon as its own row with the same osm_id: parts are joined back.
        // With repeated names (e.g. a barrio and a localidad), the most specific level wins, then the largest one.
        public static readonly string ZonaPorSlug = $"""
            partes AS (
                SELECT t.osm_id, t.name, {Nivel("t")} AS nivel, t.way, t.way_area
                FROM {Schema}.planet_osm_polygon t
                WHERE t.boundary = 'administrative' AND t.name IS NOT NULL
                  AND {Nivel("t")} BETWEEN 5 AND 10
                  AND {Slug} = @slug
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
