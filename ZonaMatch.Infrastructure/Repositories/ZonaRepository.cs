using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Zones are the administrative boundaries of the OSM import (see ZonaSql).
    // The GeoJSON is built inside PostGIS, like MapaRepository.
    public class ZonaRepository : IZonaRepository
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;

        // ~1 m precision, keeps the payload small
        private const int Decimales = 5;

        private const string Slug = ZonaSql.Slug;

        private static string Nivel(string alias) => ZonaSql.Nivel(alias);

        // The zone is chosen as in ZonaSql.ZonaPorSlug.
        // Jurisdicciones: the boundaries that contain the zone, most specific first. Only ones at least as
        // large count, and one that covers the same area as a wider one is skipped (the city "Buenos Aires", level 8,
        // repeats CABA).
        private static readonly string SqlPorSlug = $"""
            WITH {ZonaSql.ZonaPorSlug},
            centro AS (
                SELECT {ZonaSql.Centro("z.geom")} AS punto
                FROM zona z
            ),
            contenedores AS (
                SELECT j.osm_id, j.name, {Nivel("j")} AS nivel, j.way_area
                FROM {Schema}.planet_osm_polygon j, zona z
                WHERE j.boundary = 'administrative' AND j.name IS NOT NULL
                  AND {Nivel("j")} BETWEEN 4 AND z.nivel - 1
                  AND ST_Contains(j.way, ST_PointOnSurface(z.way))
                  -- a smaller boundary can also hold that point (a comuna inside the city "Buenos Aires");
                  -- one of the same size counts (Comuna 14 is exactly Palermo)
                  AND j.way_area >= 0.99 * z.way_area
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

        // Same zone choice per slug as SqlPorSlug, so every result opens the zone it shows.
        // Jurisdicciones follow the same rules too; they come as two parallel arrays (names and levels).
        private static readonly string SqlBuscar = $"""
            WITH partes AS (
                SELECT t.osm_id, t.name, {Nivel("t")} AS nivel, t.way, t.way_area, {Slug} AS slug
                FROM {Schema}.planet_osm_polygon t
                WHERE t.boundary = 'administrative' AND t.name IS NOT NULL
                  AND {Nivel("t")} BETWEEN 5 AND 10
                  AND {Slug} LIKE '%' || @fragmento || '%'
            ),
            zonas AS (
                SELECT DISTINCT ON (slug) slug, name, nivel,
                       sum(way_area) AS way_area,
                       sum(ST_Area(ST_Transform(way, 4326)::geography)) AS metros2,
                       ST_PointOnSurface((array_agg(way ORDER BY way_area DESC))[1]) AS punto
                FROM partes
                GROUP BY slug, osm_id, name, nivel
                ORDER BY slug, nivel DESC, sum(way_area) DESC
            ),
            elegidas AS (
                SELECT *, slug LIKE @fragmento || '%' AS empieza
                FROM zonas
                ORDER BY empieza DESC, length(slug), slug
                LIMIT @limite
            )
            SELECT e.slug AS "Slug",
                   e.name AS "Nombre",
                   e.nivel AS "NivelAdministrativo",
                   round((e.metros2 / 1e6)::numeric, 2)::float8 AS "SuperficieKm2",
                   COALESCE(j.nombres, ARRAY[]::text[]) AS "JurisdiccionNombres",
                   COALESCE(j.niveles, ARRAY[]::int[]) AS "JurisdiccionNiveles"
            FROM elegidas e
            LEFT JOIN LATERAL (
                WITH contenedores AS (
                    SELECT c.name, {Nivel("c")} AS nivel, c.way_area
                    FROM {Schema}.planet_osm_polygon c
                    WHERE c.boundary = 'administrative' AND c.name IS NOT NULL
                      AND {Nivel("c")} BETWEEN 4 AND e.nivel - 1
                      AND ST_Contains(c.way, e.punto)
                      AND c.way_area >= 0.99 * e.way_area
                )
                SELECT array_agg(c.name ORDER BY c.nivel DESC) AS nombres,
                       array_agg(c.nivel ORDER BY c.nivel DESC) AS niveles
                FROM contenedores c
                WHERE NOT EXISTS (
                    SELECT 1 FROM contenedores w
                    WHERE w.nivel < c.nivel AND abs(w.way_area - c.way_area) < 0.01 * w.way_area)
            ) j ON true
            ORDER BY e.empieza DESC, length(e.slug), e.slug
            """;

        private readonly ZonaMatchDbContext _dbContext;

        public ZonaRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static readonly string SqlExiste = $"""
            WITH {ZonaSql.ZonaPorSlug}
            SELECT EXISTS (SELECT 1 FROM zona) AS "Value"
            """;

        private static readonly string SqlContienePunto = $"""
            WITH {ZonaSql.ZonaPorSlug}
            SELECT EXISTS (
                SELECT 1 FROM zona z
                WHERE ST_Intersects(z.geom, ST_SetSRID(ST_MakePoint(@lon, @lat), 4326))) AS "Value"
            """;

        public Task<bool> ExisteAsync(string slug, CancellationToken cancellationToken = default) =>
            _dbContext.Database
                .SqlQueryRaw<bool>(SqlExiste, new NpgsqlParameter("slug", slug))
                .SingleAsync(cancellationToken);

        public Task<bool> ContienePuntoAsync(string slug, double latitud, double longitud, CancellationToken cancellationToken = default) =>
            _dbContext.Database
                .SqlQueryRaw<bool>(SqlContienePunto,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("lat", latitud),
                    new NpgsqlParameter("lon", longitud))
                .SingleAsync(cancellationToken);

        public async Task<string?> GetPorSlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<string>(SqlPorSlug,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("tol", tolerancia))
                .ToListAsync(cancellationToken);

            return filas.SingleOrDefault();
        }

        public async Task<IReadOnlyList<ZonaResumenDto>> BuscarAsync(
            string fragmento, int limite, CancellationToken cancellationToken = default)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<ZonaFila>(SqlBuscar,
                    new NpgsqlParameter("fragmento", fragmento),
                    new NpgsqlParameter("limite", limite))
                .ToListAsync(cancellationToken);

            return filas
                .Select(f => new ZonaResumenDto(
                    f.Slug,
                    f.Nombre,
                    f.NivelAdministrativo,
                    f.JurisdiccionNombres.Zip(f.JurisdiccionNiveles, (nombre, nivel) => new JurisdiccionDto(nombre, nivel)).ToList(),
                    f.SuperficieKm2))
                .ToList();
        }

        // Keyless result of SqlBuscar; property names match the column aliases
        private sealed class ZonaFila
        {
            public string Slug { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public int NivelAdministrativo { get; set; }
            public double SuperficieKm2 { get; set; }
            public string[] JurisdiccionNombres { get; set; } = [];
            public int[] JurisdiccionNiveles { get; set; } = [];
        }
    }
}
