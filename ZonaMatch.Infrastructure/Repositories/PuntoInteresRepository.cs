using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Points of interest read from the osm2pgsql import (schema "osm", default style, SRID 3857).
    // Nodes and polygons are both used; a polygon is represented by a point inside it (ST_PointOnSurface).
    // The OSM tags -> app category mapping lives in the CASE below.
    public class PuntoInteresRepository : IPuntoInteresRepository
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;

        // First matching rule wins (e.g. a pharmacy is "comercios", not "salud")
        private const string Clasificacion = $"""
            CASE
                WHEN railway IN ('station', 'halt', 'tram_stop', 'subway_entrance')
                     OR public_transport = 'station'
                     OR amenity = 'bus_station'
                     OR highway = 'bus_stop' THEN '{CategoriaPuntoInteres.Transporte}'
                WHEN amenity IN ('kindergarten', 'school', 'college', 'university') THEN '{CategoriaPuntoInteres.Educacion}'
                WHEN amenity IN ('hospital', 'clinic', 'doctors', 'dentist') THEN '{CategoriaPuntoInteres.Salud}'
                WHEN shop IS NOT NULL
                     OR amenity IN ('pharmacy', 'bank', 'marketplace', 'post_office') THEN '{CategoriaPuntoInteres.Comercios}'
                WHEN leisure IN ('sports_centre', 'fitness_centre', 'stadium', 'swimming_pool') THEN '{CategoriaPuntoInteres.Deporte}'
                WHEN leisure IN ('park', 'garden', 'nature_reserve') THEN '{CategoriaPuntoInteres.EspaciosVerdes}'
                WHEN amenity IN ('restaurant', 'cafe', 'bar', 'pub', 'fast_food', 'ice_cream',
                                 'theatre', 'cinema', 'arts_centre', 'library')
                     OR tourism IN ('museum', 'gallery') THEN '{CategoriaPuntoInteres.Cultura}'
            END
            """;

        // Value of the tag that classified the point, same priority as above
        private const string Tipo = """
            CASE
                WHEN railway IN ('station', 'halt', 'tram_stop', 'subway_entrance') THEN railway
                WHEN highway = 'bus_stop' THEN highway
                WHEN amenity IS NOT NULL THEN amenity
                WHEN shop IS NOT NULL THEN shop
                WHEN public_transport IS NOT NULL THEN public_transport
                ELSE COALESCE(leisure, tourism)
            END
            """;

        private const string Columnas = "name, amenity, shop, leisure, tourism, railway, public_transport, highway, operator";

        private const string App = ZonaMatchDbOptions.AppSchema;

        // Classified points inside an area, with their distance to a reference point. Shared by the list and the
        // summary, for a radius and for a zone. "origen" defines the CTE "o" (prefilter geometry g in 3857 and the
        // reference geography geog); candidates pass the index-friendly "prefiltro" and then the exact "dentro"
        // (on the 4326 point c.punto). Ends with the "en_area" CTE.
        // Los aportes de vecinos aprobados por otros vecinos (app.aportes) se aplican sobre OSM: los puntos nuevos
        // se agregan ("a" + id) y las correcciones de un lugar cambian su nombre, categoria o ubicacion, o lo esconden
        // si cerro. Una correccion por motivo "otro" es solo informativa.
        private static string PuntosEn(string origen, Func<string, string> prefiltro, string dentro) => $"""
            WITH {origen},
            candidatos AS (
                SELECT 'n' || t.osm_id AS id, {Columnas}, t.way AS punto
                FROM {Schema}.planet_osm_point t, o
                WHERE {prefiltro("t.way")}
                UNION ALL
                SELECT CASE WHEN t.osm_id < 0 THEN 'r' || -t.osm_id ELSE 'w' || t.osm_id END, {Columnas},
                       ST_PointOnSurface(t.way)
                FROM {Schema}.planet_osm_polygon t, o
                WHERE {prefiltro("t.way")}
            ),
            clasificados AS (
                SELECT id, name, operator, {Clasificacion} AS categoria, {Tipo} AS tipo, ST_Transform(punto, 4326) AS punto,
                       'osm' AS fuente
                FROM candidatos
                WHERE name IS NOT NULL OR highway = 'bus_stop' OR railway = 'subway_entrance'
                UNION ALL
                SELECT 'a' || a.id::text, a.nombre, NULL, a.categoria, NULL, a.ubicacion, 'vecinos'
                FROM {App}.aportes a
                WHERE a.tipo = 'PuntoNuevo' AND a.estado = 'Aprobado'
            ),
            -- La ultima correccion aprobada de cada lugar y motivo
            correcciones AS (
                SELECT DISTINCT ON (a.punto_interes_id, a.motivo) a.punto_interes_id, a.motivo, a.nombre, a.categoria, a.ubicacion
                FROM {App}.aportes a
                WHERE a.tipo = 'Correccion' AND a.estado = 'Aprobado'
                ORDER BY a.punto_interes_id, a.motivo, a.fecha_resolucion DESC
            ),
            corregidos AS (
                SELECT c.id,
                       COALESCE(cn.nombre, c.name) AS name,
                       c.operator,
                       COALESCE(cc.categoria, c.categoria) AS categoria,
                       -- el tipo de OSM ya no describe a un lugar que paso a otra categoria
                       CASE WHEN cc.categoria IS NULL THEN c.tipo END AS tipo,
                       COALESCE(cu.ubicacion, c.punto) AS punto,
                       c.fuente
                FROM clasificados c
                LEFT JOIN correcciones cn ON cn.punto_interes_id = c.id AND cn.motivo = 'Nombre'
                LEFT JOIN correcciones cc ON cc.punto_interes_id = c.id AND cc.motivo = 'Categoria'
                LEFT JOIN correcciones cu ON cu.punto_interes_id = c.id AND cu.motivo = 'Ubicacion'
                WHERE NOT EXISTS (
                    SELECT 1 FROM correcciones x WHERE x.punto_interes_id = c.id AND x.motivo = 'Cerro')
            ),
            en_area AS (
                SELECT c.*, ST_Distance(c.punto::geography, o.geog) AS distancia
                FROM corregidos c, o
                WHERE c.categoria IS NOT NULL
                  AND {dentro}
            )
            """;

        // Web Mercator meters are stretched by 1/cos(lat): the radius is scaled by k for the ST_DWithin prefilter,
        // then the exact distance is measured on the geography
        private static readonly string PuntosEnRadio = PuntosEn(
            """
            o AS (
                SELECT ST_Transform(ST_SetSRID(ST_MakePoint(@lon, @lat), 4326), 3857) AS g,
                       ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography AS geog,
                       1 / cos(radians(@lat)) AS k
            )
            """,
            way => $"ST_DWithin({way}, o.g, @radio * o.k)",
            "ST_DWithin(c.punto::geography, o.geog, @radio)");

        // The zone of the @slug parameter (same choice as GET /Zonas/{slug}); distances are measured from its center.
        // A polygon counts when the point that represents it falls inside the zone.
        private static readonly string PuntosEnZona = PuntosEn(
            $"""
            {ZonaSql.ZonaPorSlug},
            o AS (
                SELECT z.way AS g, z.geom, ({ZonaSql.Centro("z.geom")})::geography AS geog
                FROM zona z
            )
            """,
            way => $"ST_Intersects({way}, o.g)",
            "ST_Intersects(c.punto, o.geom)");

        private static string Cercanos(string puntos) => $"""
            {puntos}
            SELECT id AS "Id",
                   name AS "Nombre",
                   categoria AS "Categoria",
                   tipo AS "Tipo",
                   operator AS "Operador",
                   ST_Y(punto) AS "Latitud",
                   ST_X(punto) AS "Longitud",
                   distancia AS "DistanciaMetros",
                   fuente AS "Fuente"
            FROM en_area
            WHERE categoria = ANY(@categorias)
            ORDER BY distancia
            LIMIT @limite
            """;

        private static string Resumen(string puntos) => $"""
            {puntos}
            SELECT categoria AS "Categoria", tipo AS "Tipo", count(*)::int AS "Cantidad"
            FROM en_area
            GROUP BY categoria, tipo
            """;

        private static readonly string SqlCercanos = Cercanos(PuntosEnRadio);
        private static readonly string SqlResumen = Resumen(PuntosEnRadio);
        private static readonly string SqlEnZona = Cercanos(PuntosEnZona);
        private static readonly string SqlResumenZona = Resumen(PuntosEnZona);

        private readonly ZonaMatchDbContext _dbContext;

        public PuntoInteresRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<IReadOnlyList<PuntoInteresDto>> GetCercanosAsync(
            double latitud, double longitud, double radioMetros, IReadOnlyCollection<string> categorias, int limite,
            CancellationToken cancellationToken = default) =>
            ListarAsync(SqlCercanos, cancellationToken,
                new NpgsqlParameter("lat", latitud),
                new NpgsqlParameter("lon", longitud),
                new NpgsqlParameter("radio", radioMetros),
                new NpgsqlParameter("categorias", categorias.ToArray()),
                new NpgsqlParameter("limite", limite));

        public Task<IReadOnlyList<CantidadPorTipo>> ContarPorTipoAsync(
            double latitud, double longitud, double radioMetros, CancellationToken cancellationToken = default) =>
            ContarAsync(SqlResumen, cancellationToken,
                new NpgsqlParameter("lat", latitud),
                new NpgsqlParameter("lon", longitud),
                new NpgsqlParameter("radio", radioMetros));

        public Task<IReadOnlyList<PuntoInteresDto>> GetEnZonaAsync(
            string slug, IReadOnlyCollection<string> categorias, int limite, CancellationToken cancellationToken = default) =>
            ListarAsync(SqlEnZona, cancellationToken,
                new NpgsqlParameter("slug", slug),
                new NpgsqlParameter("categorias", categorias.ToArray()),
                new NpgsqlParameter("limite", limite));

        public Task<IReadOnlyList<CantidadPorTipo>> ContarPorTipoEnZonaAsync(
            string slug, CancellationToken cancellationToken = default) =>
            ContarAsync(SqlResumenZona, cancellationToken, new NpgsqlParameter("slug", slug));

        private async Task<IReadOnlyList<PuntoInteresDto>> ListarAsync(
            string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parametros)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<PuntoInteresFila>(sql, parametros)
                .ToListAsync(cancellationToken);

            return filas
                .Select(f => new PuntoInteresDto(
                    f.Id, f.Nombre, f.Categoria, f.Tipo, f.Operador, f.Latitud, f.Longitud, f.DistanciaMetros, f.Fuente))
                .ToList();
        }

        private async Task<IReadOnlyList<CantidadPorTipo>> ContarAsync(
            string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parametros)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<CantidadFila>(sql, parametros)
                .ToListAsync(cancellationToken);

            return filas.Select(f => new CantidadPorTipo(f.Categoria, f.Tipo, f.Cantidad)).ToList();
        }

        // Keyless results of the queries above; property names match the column aliases
        private sealed class CantidadFila
        {
            public string Categoria { get; set; } = string.Empty;
            public string? Tipo { get; set; }
            public int Cantidad { get; set; }
        }

        private sealed class PuntoInteresFila
        {
            public string Id { get; set; } = string.Empty;
            public string? Nombre { get; set; }
            public string Categoria { get; set; } = string.Empty;
            public string? Tipo { get; set; }
            public string? Operador { get; set; }
            public double Latitud { get; set; }
            public double Longitud { get; set; }
            public double DistanciaMetros { get; set; }
            public string Fuente { get; set; } = string.Empty;
        }
    }
}
