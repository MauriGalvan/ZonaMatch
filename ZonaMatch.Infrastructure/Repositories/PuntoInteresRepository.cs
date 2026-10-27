using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.ZoneRecommendations;
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
        // The approved community contributions, persisted in app.community_pois, are applied over OSM: the points
        // added by users ("a" + contribution id) join the candidates, and the changes to an OSM point replace its
        // name, category or location, or hide it if it closed.
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
                SELECT p.id, p.name, NULL, p.category, NULL, p.location, 'vecinos'
                FROM {App}.community_pois p
                WHERE p.is_new AND NOT p.removed
            ),
            corregidos AS (
                SELECT c.id,
                       COALESCE(p.name, c.name) AS name,
                       c.operator,
                       COALESCE(p.category, c.categoria) AS categoria,
                       -- the OSM type no longer describes a place moved to another category
                       CASE WHEN p.category IS NULL OR p.is_new THEN c.tipo END AS tipo,
                       COALESCE(p.location, c.punto) AS punto,
                       c.fuente
                FROM clasificados c
                LEFT JOIN {App}.community_pois p ON p.id = c.id
                WHERE p.removed IS NOT TRUE
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

        private static readonly string SqlProfileRecommendations = $"""
            WITH {ZonaSql.ZonaPorSlug},
            zone_area AS (
                SELECT z.way AS geometry_3857, z.geom AS geometry_4326,
                       -- Distances from the point chosen on the map, or from the zone center without one
                       COALESCE(ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326), {ZonaSql.Centro("z.geom")})::geography AS center
                FROM zona z
            ),
            raw_candidates AS (
                SELECT 'n' || t.osm_id AS id, t.name, t.amenity, t.shop, t.way AS location_3857
                FROM {Schema}.planet_osm_point t
                CROSS JOIN zone_area z
                WHERE t.name IS NOT NULL AND ST_Intersects(t.way, z.geometry_3857)
                UNION ALL
                SELECT CASE WHEN t.osm_id < 0 THEN 'r' || -t.osm_id ELSE 'w' || t.osm_id END,
                       t.name, t.amenity, t.shop, ST_PointOnSurface(t.way)
                FROM {Schema}.planet_osm_polygon t
                CROSS JOIN zone_area z
                WHERE t.name IS NOT NULL AND ST_Intersects(t.way, z.geometry_3857)
            ),
            deduplicated AS (
                SELECT DISTINCT ON (c.id) c.id, c.name, c.amenity, c.shop,
                       ST_Transform(c.location_3857, 4326) AS location_4326,
                       ST_Distance(ST_Transform(c.location_3857, 4326)::geography, z.center) AS distance_meters
                FROM raw_candidates c
                CROSS JOIN zone_area z
                WHERE ST_Intersects(ST_Transform(c.location_3857, 4326), z.geometry_4326)
                ORDER BY c.id, ST_Distance(ST_Transform(c.location_3857, 4326)::geography, z.center)
            ),
            matches AS (
                SELECT c.id, c.name, requested.category,
                       ST_Y(c.location_4326) AS latitude,
                       ST_X(c.location_4326) AS longitude,
                       c.distance_meters
                FROM deduplicated c
                CROSS JOIN unnest(@categories::text[]) AS requested(category)
                WHERE
                    (requested.category = '{ProfileRecommendationCategories.MaternalSchool}'
                     AND c.amenity IN ('childcare', 'kindergarten', 'school', 'college', 'university')
                     AND (
                         c.name ILIKE '%maternal%'
                         OR c.name ILIKE '%materno infantil%'
                         OR c.name ILIKE '%materno-infantil%'
                         OR c.name ILIKE '%centro de primera infancia%'
                         OR c.name ILIKE '%centro primera infancia%'
                         OR c.name ILIKE '%espacio de primera infancia%'
                         OR c.name ILIKE '%espacio primera infancia%'
                         OR c.name ILIKE '%primera infancia%'
                         OR c.name ILIKE '%primera-infancia%'
                         OR c.name ILIKE '%guarderia%'
                         OR c.name ILIKE '%guardería%'
                         OR c.name ILIKE '%sala cuna%'
                         OR c.name ILIKE '%sala-cuna%'
                         OR c.name ~* '(^|[^[:alnum:]])CPI([^[:alnum:]]|$)'
                     ))
                    OR (requested.category = '{ProfileRecommendationCategories.Kindergarten}'
                     AND c.amenity IN ('childcare', 'kindergarten', 'school', 'college', 'university')
                     AND (c.name ILIKE '%jardin%' OR c.name ILIKE '%jardín%'))
                    OR (requested.category = '{ProfileRecommendationCategories.PrimarySchool}'
                     AND c.amenity IN ('childcare', 'kindergarten', 'school', 'college', 'university')
                     AND c.name ILIKE '%primari%')
                    OR (requested.category = '{ProfileRecommendationCategories.SecondarySchool}'
                     AND c.amenity IN ('childcare', 'kindergarten', 'school', 'college', 'university')
                     AND c.name ILIKE '%secundari%')
                    OR (requested.category = '{ProfileRecommendationCategories.Veterinary}'
                     AND (c.amenity = 'veterinary' OR c.name ILIKE '%veterinar%'))
                    OR (requested.category = '{ProfileRecommendationCategories.CarRepair}'
                     AND (c.shop IN ('car_repair', 'vehicle') OR c.amenity = 'car_repair'
                          OR c.name ILIKE '%mecanic%' OR c.name ILIKE '%mecánic%'))
                    OR (requested.category = '{ProfileRecommendationCategories.BicycleShop}'
                     AND (c.shop = 'bicycle' OR c.name ILIKE '%biciclet%'))
            ),
            ranked AS (
                SELECT *,
                       row_number() OVER (PARTITION BY category ORDER BY distance_meters, name, id) AS position,
                       count(*) OVER (PARTITION BY category) AS total
                FROM matches
            )
            SELECT id AS "Id",
                   name AS "Name",
                   category AS "Category",
                   latitude AS "Latitude",
                   longitude AS "Longitude",
                   distance_meters AS "DistanceMeters",
                   (total > @limit) AS "IsTruncated"
            FROM ranked
            WHERE position <= @limit
            ORDER BY category, distance_meters, name, id
            """;

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

        public async Task<IReadOnlyList<ProfileRecommendationCandidate>> GetProfileRecommendationsInZoneAsync(
            string slug,
            IReadOnlyCollection<string> categories,
            int limitPerCategory,
            (double Latitude, double Longitude)? origin = null,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Database
                .SqlQueryRaw<ProfileRecommendationCandidate>(
                    SqlProfileRecommendations,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("categories", categories.ToArray()),
                    new NpgsqlParameter("limit", limitPerCategory),
                    new NpgsqlParameter("latitude", NpgsqlDbType.Double) { Value = (object?)origin?.Latitude ?? DBNull.Value },
                    new NpgsqlParameter("longitude", NpgsqlDbType.Double) { Value = (object?)origin?.Longitude ?? DBNull.Value })
                .ToListAsync(cancellationToken);
        }

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
