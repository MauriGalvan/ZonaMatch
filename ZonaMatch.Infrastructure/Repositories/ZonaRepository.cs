using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Zones are the administrative boundaries of the osm2pgsql import (schema "osm", SRID 3857).
    // admin_level in the AMBA extract: 4 CABA, 5 comunas and partidos, 8 localidades, 9 and 10 barrios.
    // The province of Buenos Aires is not there: the AMBA bounding box cuts it, so osm2pgsql cannot build it.
    // The GeoJSON is built inside PostGIS, like MapaRepository.
    public class ZonaRepository : IZonaRepository
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;

        // ~1 m precision, keeps the payload small
        private const int Decimales = 5;

        // Same rule as SlugZona.Generar: lowercase, no accents, non-alphanumeric runs -> "-"
        private const string Slug = """
            trim(both '-' from regexp_replace(
                translate(lower(t.name), 'áàäâãéèëêíìïîóòöôõúùüûñç', 'aaaaaeeeeiiiiooooouuuunc'),
                '[^a-z0-9]+', '-', 'g'))
            """;

        // admin_level is free text in OSM; the CASE keeps a bad value from breaking the cast
        private static string Nivel(string alias) => $"CASE WHEN {alias}.admin_level ~ '^[0-9]+$' THEN {alias}.admin_level::int END";

        // osm2pgsql stores each part of a multipolygon as its own row with the same osm_id: parts are joined back.
        // With repeated names (e.g. a barrio and a localidad), the most specific level wins, then the largest one.
        // Jurisdicciones: the boundaries that contain the zone, most specific first. Only larger ones
        // count, and one that covers the same area as a wider one is skipped (the city "Buenos Aires", level 8,
        // repeats CABA).
        // Centro: the centroid, or a point inside the zone when the centroid falls outside (concave shapes).
        private static readonly string SqlPorSlug = $"""
            WITH partes AS (
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
            ),
            centro AS (
                SELECT CASE WHEN ST_Contains(z.geom, ST_Centroid(z.geom)) THEN ST_Centroid(z.geom)
                            ELSE ST_PointOnSurface(z.geom) END AS punto
                FROM zona z
            ),
            contenedores AS (
                SELECT j.osm_id, j.name, {Nivel("j")} AS nivel, j.way_area
                FROM {Schema}.planet_osm_polygon j, zona z
                WHERE j.boundary = 'administrative' AND j.name IS NOT NULL
                  AND {Nivel("j")} BETWEEN 4 AND z.nivel - 1
                  AND ST_Contains(j.way, ST_PointOnSurface(z.way))
                  -- a smaller boundary can also hold that point (a comuna inside the city "Buenos Aires")
                  AND j.way_area > 1.01 * z.way_area
            ),
            jurisdicciones AS (
                SELECT j.name, j.nivel
                FROM contenedores j
                WHERE NOT EXISTS (
                    SELECT 1 FROM contenedores w
                    WHERE w.nivel < j.nivel AND abs(w.way_area - j.way_area) < 0.01 * w.way_area)
            )
            SELECT json_build_object(
                'type', 'Feature',
                'id', CASE WHEN z.osm_id < 0 THEN 'r' || -z.osm_id ELSE 'w' || z.osm_id END,
                'properties', json_build_object(
                    'slug', @slug,
                    'nombre', z.name,
                    'nivelAdministrativo', z.nivel,
                    'jurisdicciones', (
                        SELECT COALESCE(json_agg(json_build_object('nombre', j.name, 'nivelAdministrativo', j.nivel)
                                                 ORDER BY j.nivel DESC), '[]'::json)
                        FROM jurisdicciones j),
                    'superficieKm2', round((ST_Area(z.geom::geography) / 1e6)::numeric, 2),
                    'centro', json_build_object('latitud', ST_Y(c.punto), 'longitud', ST_X(c.punto))),
                'geometry', ST_AsGeoJSON(ST_SimplifyPreserveTopology(z.geom, @tol), {Decimales})::json)::text AS "Value"
            FROM zona z, centro c
            """;

        private readonly ZonaMatchDbContext _dbContext;

        public ZonaRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string?> GetPorSlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<string>(SqlPorSlug,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("tol", tolerancia))
                .ToListAsync(cancellationToken);

            return filas.SingleOrDefault();
        }
    }
}
