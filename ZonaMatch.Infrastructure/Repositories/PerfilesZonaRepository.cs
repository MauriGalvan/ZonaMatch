using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Puntaje;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Arma el perfil de todas las zonas elegidas de geo.zonas para el motor de puntaje: censo y delitos
    // (geo.zona_indicadores), transporte (geo.zona_transporte_*), equipamiento de OSM y alquiler
    // (app.zone_market). Contar el equipamiento de todas las zonas tarda unos segundos, asi que el resultado
    // se guarda en PerfilesZonaCache: los datos solo cambian cuando se vuelve a correr el ETL.
    public class PerfilesZonaRepository : IPerfilesZonaRepository
    {
        private const string Geo = ZonaMatchDbOptions.GeoSchema;
        private const string Osm = ZonaMatchDbOptions.OsmSchema;
        private const string App = ZonaMatchDbOptions.AppSchema;

        // Mismas etiquetas que las categorias de PuntoInteresRepository (educacion, salud, espacios verdes).
        // Los poligonos cuentan por un punto interior; la superficie verde es la de los parques dentro de la zona,
        // en m2 reales (el area en 3857 se corrige por cos^2 de la latitud).
        private static readonly string SqlPerfiles = $"""
            WITH zonas AS (
                SELECT DISTINCT ON (z.slug) z.osm_id, z.slug, z.nombre, z.nivel, z.superficie_km2, z.geom,
                       ST_Transform(z.geom, 3857) AS way
                FROM {Geo}.zonas z
                WHERE z.elegida
                  -- This OSM locality is a duplicate representation of all CABA; other same-area
                  -- zones (for example neighborhoods matching their comuna) are valid ranking zones.
                  AND z.slug <> 'buenos-aires'
                ORDER BY z.slug, z.nivel DESC
            ),
            etiquetados AS (
                SELECT amenity, leisure, way FROM {Osm}.planet_osm_point
                WHERE name IS NOT NULL
                  AND (amenity IN ('kindergarten', 'school', 'college', 'university', 'hospital', 'clinic', 'doctors')
                       OR leisure IN ('park', 'garden', 'nature_reserve'))
                UNION ALL
                SELECT amenity, leisure, ST_PointOnSurface(way) FROM {Osm}.planet_osm_polygon
                WHERE name IS NOT NULL
                  AND (amenity IN ('kindergarten', 'school', 'college', 'university', 'hospital', 'clinic', 'doctors')
                       OR leisure IN ('park', 'garden', 'nature_reserve'))
            ),
            puntos AS (
                SELECT CASE
                           WHEN amenity IN ('kindergarten', 'school', 'college', 'university') THEN 'educacion'
                           WHEN amenity IN ('hospital', 'clinic', 'doctors') THEN 'salud'
                           ELSE 'verde'
                       END AS categoria, way
                FROM etiquetados
            ),
            equipamiento AS (
                SELECT z.osm_id,
                       count(*) FILTER (WHERE p.categoria = 'educacion')::int AS escuelas,
                       count(*) FILTER (WHERE p.categoria = 'salud')::int AS salud,
                       count(*) FILTER (WHERE p.categoria = 'verde')::int AS verdes
                FROM zonas z
                LEFT JOIN puntos p ON ST_Intersects(p.way, z.way)
                GROUP BY z.osm_id
            ),
            verde AS (
                SELECT z.osm_id,
                       COALESCE(sum(ST_Area(ST_Intersection(ST_MakeValid(v.way), z.way))
                                    * cos(radians(ST_Y(ST_Centroid(z.geom)))) ^ 2), 0) AS m2
                FROM zonas z
                LEFT JOIN {Osm}.planet_osm_polygon v
                       ON v.leisure IN ('park', 'garden', 'nature_reserve') AND ST_Intersects(v.way, z.way)
                GROUP BY z.osm_id
            ),
            lineas AS (
                SELECT osm_id, sum(lineas)::int AS lineas FROM {Geo}.zona_transporte_paradas GROUP BY osm_id
            ),
            pico AS (
                SELECT osm_id, sum(salidas)::int AS salidas FROM {Geo}.zona_transporte_lineas
                WHERE franja = 'manana' GROUP BY osm_id
            )
            SELECT z.slug AS "Slug", z.nombre AS "Nombre", z.nivel AS "Nivel", z.superficie_km2 AS "SuperficieKm2",
                   COALESCE(i.poblacion, 0) AS "Poblacion", i.delitos_comparables AS "Delitos",
                   COALESCE(l.lineas, 0) AS "LineasTransporte", COALESCE(p.salidas, 0) AS "SalidasHoraPico",
                   e.escuelas AS "Escuelas", e.salud AS "CentrosSalud", e.verdes AS "EspaciosVerdes",
                   v.m2 AS "SuperficieVerdeM2",
                   ST_Y({ZonaSql.Centro("z.geom")}) AS "Latitud", ST_X({ZonaSql.Centro("z.geom")}) AS "Longitud"
            FROM zonas z
            LEFT JOIN {Geo}.zona_indicadores i USING (osm_id)
            LEFT JOIN lineas l USING (osm_id)
            LEFT JOIN pico p USING (osm_id)
            JOIN equipamiento e USING (osm_id)
            JOIN verde v USING (osm_id)
            """;

        // Alquiler y venta de cada zona (app.zone_market), por operacion y tipo de vivienda ("all" son todos juntos):
        // alquiler y expensas en pesos, venta en dolares. Los partidos y comunas sin fila propia para una combinacion
        // suman las zonas que contienen (promedio ponderado por propiedades publicadas).
        private static readonly string SqlMercado = $"""
            WITH zonas AS (
                SELECT DISTINCT ON (z.slug) z.slug, z.nivel, z.geom
                FROM {Geo}.zonas z
                WHERE z.elegida AND z.slug <> 'buenos-aires'
                ORDER BY z.slug, z.nivel DESC
            ),
            filas AS (
                SELECT zone_slug, operation, property_type, average_price, average_expenses_ars, average_price_m2, listings
                FROM {App}.zone_market
                WHERE average_price IS NOT NULL
            ),
            propias AS (
                SELECT z.slug, f.operation, f.property_type, f.average_price AS promedio, f.listings AS propiedades,
                       f.average_expenses_ars AS expensas, f.average_price_m2 AS precio_m2
                FROM zonas z
                JOIN filas f ON f.zone_slug = z.slug
            ),
            sumadas AS (
                SELECT z.slug, b.operation, b.property_type,
                       round(sum(b.average_price * greatest(b.listings, 1)) / sum(greatest(b.listings, 1))) AS promedio,
                       sum(b.listings)::int AS propiedades,
                       round(sum(b.average_expenses_ars * greatest(b.listings, 1))
                             / nullif(sum(greatest(b.listings, 1)) FILTER (WHERE b.average_expenses_ars IS NOT NULL), 0)) AS expensas,
                       round(sum(b.average_price_m2 * greatest(b.listings, 1))
                             / nullif(sum(greatest(b.listings, 1)) FILTER (WHERE b.average_price_m2 IS NOT NULL), 0)) AS precio_m2
                FROM zonas z
                JOIN zonas h ON h.nivel > 5 AND ST_Contains(z.geom, ST_PointOnSurface(h.geom))
                JOIN filas b ON b.zone_slug = h.slug
                WHERE z.nivel <= 5
                  AND NOT EXISTS (SELECT 1 FROM propias p
                                  WHERE p.slug = z.slug AND p.operation = b.operation AND p.property_type = b.property_type)
                GROUP BY z.slug, b.operation, b.property_type
            )
            SELECT slug AS "Slug", operation AS "Operacion", property_type AS "Tipo", promedio AS "Promedio",
                   propiedades AS "Propiedades", expensas AS "Expensas", precio_m2 AS "PrecioM2"
            FROM propias
            UNION ALL
            SELECT slug, operation, property_type, promedio, propiedades, expensas, precio_m2
            FROM sumadas
            """;

        // Precios por cantidad de ambientes (app.zone_market_rooms), solo los grupos con muestra suficiente. Las
        // zonas heredadas y los partidos y comunas ya vienen calculados por el ETL.
        private static readonly string SqlMercadoAmbientes = $"""
            SELECT zone_slug AS "Slug", operation AS "Operacion", property_type AS "Tipo", rooms AS "Ambientes",
                   average_price AS "Promedio", listings AS "Propiedades", average_expenses_ars AS "Expensas",
                   average_price_m2 AS "PrecioM2"
            FROM {App}.zone_market_rooms
            WHERE average_price IS NOT NULL AND sample_size >= {ZoneMarketRooms.MinSampleSize}
            """;

        private static readonly string SqlZonasDelEntorno = $"""
            WITH zonas_validas AS (
                -- Buenos Aires repeats the full CABA boundary; keep other same-area neighborhoods.
                SELECT z.*
                FROM {Geo}.zonas z
                WHERE z.elegida
                  AND z.slug <> 'buenos-aires'
            ),
            puntos AS (
                SELECT p.orden, ST_SetSRID(ST_MakePoint(p.longitud, p.latitud), 4326) AS geom
                FROM unnest(@latitudes::double precision[], @longitudes::double precision[])
                     WITH ORDINALITY AS p(latitud, longitud, orden)
            ),
            contenedoras AS (
                SELECT p.orden, z.osm_id, z.slug, z.nivel, z.geom
                FROM puntos p
                JOIN zonas_validas z ON ST_Covers(z.geom, p.geom)
            ),
            mas_especificas AS (
                SELECT c.*
                FROM contenedoras c
                WHERE c.nivel = (
                    SELECT max(c2.nivel)
                    FROM contenedoras c2
                    WHERE c2.orden = c.orden
                )
            ),
            segmentos AS (
                SELECT a.orden AS origen, b.orden AS destino,
                       ST_MakeLine(a.geom, b.geom) AS geom,
                       greatest(za.nivel, zb.nivel) AS nivel
                FROM puntos a
                JOIN puntos b ON a.orden < b.orden
                JOIN mas_especificas za ON za.orden = a.orden
                JOIN mas_especificas zb ON zb.orden = b.orden
            )
            SELECT DISTINCT candidatas.slug AS "Value"
            FROM (
                SELECT vecina.slug
                FROM mas_especificas base
                JOIN zonas_validas vecina
                  ON vecina.nivel = base.nivel
                 AND ST_Intersects(vecina.geom, base.geom)

                UNION

                SELECT intermedia.slug
                FROM segmentos tramo
                JOIN zonas_validas intermedia
                  ON intermedia.nivel = tramo.nivel
                 AND ST_Intersects(intermedia.geom, tramo.geom)
            ) candidatas
            """;

        private readonly ZonaMatchDbContext _dbContext;
        private readonly PerfilesZonaCache _cache;
        private readonly ILogger<PerfilesZonaRepository> _logger;

        public PerfilesZonaRepository(ZonaMatchDbContext dbContext, PerfilesZonaCache cache, ILogger<PerfilesZonaRepository> logger)
        {
            _dbContext = dbContext;
            _cache = cache;
            _logger = logger;
        }

        public Task<IReadOnlyList<PerfilZona>> ListarAsync(CancellationToken cancellationToken = default) =>
            _cache.ObtenerAsync(CalcularAsync, cancellationToken);

        public async Task<IReadOnlyList<string>> ListarZonasDelEntornoAsync(
            IReadOnlyCollection<(double Latitud, double Longitud)> ubicaciones,
            CancellationToken cancellationToken = default)
        {
            if (ubicaciones.Count == 0)
                return [];

            var latitudes = ubicaciones.Select(point => point.Latitud).ToArray();
            var longitudes = ubicaciones.Select(point => point.Longitud).ToArray();

            try
            {
                return await _dbContext.Database
                    .SqlQueryRaw<string>(
                        SqlZonasDelEntorno,
                        new NpgsqlParameter("latitudes", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = latitudes },
                        new NpgsqlParameter("longitudes", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = longitudes })
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning("Faltan las tablas de zonas ({Mensaje}). Correr DATOS-ZONAS-DOCKER.bat.", ex.MessageText);
                return [];
            }
        }

        private async Task<IReadOnlyList<PerfilZona>> CalcularAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Recorre todo el AMBA: mas tiempo que el comando por defecto
                _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));

                var filas = await _dbContext.Database
                    .SqlQueryRaw<PerfilFila>(SqlPerfiles)
                    .ToListAsync(cancellationToken);
                var mercado = (await _dbContext.Database
                        .SqlQueryRaw<MercadoFila>(SqlMercado)
                        .ToListAsync(cancellationToken))
                    .ToLookup(f => f.Slug, StringComparer.Ordinal);
                var ambientes = (await LeerMercadoAmbientesAsync(cancellationToken))
                    .ToLookup(f => f.Slug, StringComparer.Ordinal);

                return filas.Select(f => f.APerfil(mercado[f.Slug], ambientes[f.Slug])).ToList();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning("Faltan tablas para el puntaje de zonas ({Mensaje}). Correr DATOS-ZONAS-DOCKER.bat.", ex.MessageText);
                return [];
            }
        }

        // Sin la tabla por ambientes (migracion AddZoneMarketByRooms sin aplicar) el puntaje sigue con los promedios
        private async Task<IReadOnlyList<MercadoAmbientesFila>> LeerMercadoAmbientesAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _dbContext.Database
                    .SqlQueryRaw<MercadoAmbientesFila>(SqlMercadoAmbientes)
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning("Falta app.zone_market_rooms ({Mensaje}): aplicar las migraciones.", ex.MessageText);
                return [];
            }
        }

        // Resultado sin clave de SqlPerfiles; los nombres coinciden con los alias de las columnas
        private sealed class PerfilFila
        {
            public string Slug { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public int Nivel { get; set; }
            public double SuperficieKm2 { get; set; }
            public int Poblacion { get; set; }
            public int? Delitos { get; set; }
            public int LineasTransporte { get; set; }
            public int SalidasHoraPico { get; set; }
            public int Escuelas { get; set; }
            public int CentrosSalud { get; set; }
            public int EspaciosVerdes { get; set; }
            public double SuperficieVerdeM2 { get; set; }
            public double Latitud { get; set; }
            public double Longitud { get; set; }

            public PerfilZona APerfil(IEnumerable<MercadoFila> mercado, IEnumerable<MercadoAmbientesFila> ambientes)
            {
                var porClave = mercado.ToDictionary(f => (f.Operacion, f.Tipo));
                var porAmbientes = ambientes
                    .GroupBy(f => (f.Tipo, f.Ambientes))
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var alquiler = g.FirstOrDefault(f => f.Operacion == "rent");
                            var venta = g.FirstOrDefault(f => f.Operacion == "sale");
                            return new MercadoVivienda(
                                alquiler?.Promedio, alquiler?.Propiedades, alquiler?.Expensas, venta?.Promedio, venta?.Propiedades,
                                alquiler?.PrecioM2, venta?.PrecioM2);
                        });

                MercadoVivienda Vivienda(string tipo)
                {
                    var alquiler = porClave.GetValueOrDefault(("rent", tipo));
                    var venta = porClave.GetValueOrDefault(("sale", tipo));
                    return new MercadoVivienda(
                        alquiler?.Promedio, alquiler?.Propiedades, alquiler?.Expensas, venta?.Promedio, venta?.Propiedades,
                        alquiler?.PrecioM2, venta?.PrecioM2);
                }

                var todos = Vivienda(TiposVivienda.Todos);

                return new PerfilZona
                {
                Slug = Slug,
                Nombre = Nombre,
                Nivel = Nivel,
                SuperficieKm2 = SuperficieKm2,
                Poblacion = Poblacion,
                Delitos = Delitos,
                LineasTransporte = LineasTransporte,
                SalidasHoraPico = SalidasHoraPico,
                Escuelas = Escuelas,
                CentrosSalud = CentrosSalud,
                EspaciosVerdes = EspaciosVerdes,
                SuperficieVerdeM2 = SuperficieVerdeM2,
                Latitud = Latitud,
                Longitud = Longitud,
                AlquilerPromedio = todos.AlquilerPromedio,
                PropiedadesDisponibles = todos.PropiedadesEnAlquiler,
                ExpensasPromedio = todos.ExpensasPromedio,
                VentaPromedioUsd = todos.VentaPromedioUsd,
                PropiedadesEnVenta = todos.PropiedadesEnVenta,
                AlquilerM2 = todos.AlquilerM2,
                VentaM2Usd = todos.VentaM2Usd,
                MercadoPorTipo = new[] { TiposVivienda.Departamento, TiposVivienda.Casa, TiposVivienda.Ph }
                    .ToDictionary(tipo => tipo, Vivienda),
                MercadoPorAmbientes = porAmbientes,
                };
            }
        }

        // Resultado sin clave de SqlMercado: operacion rent|sale y tipo de vivienda de app.zone_market
        private sealed class MercadoFila
        {
            public string Slug { get; set; } = string.Empty;
            public string Operacion { get; set; } = string.Empty;
            public string Tipo { get; set; } = string.Empty;
            public decimal? Promedio { get; set; }
            public int? Propiedades { get; set; }
            public decimal? Expensas { get; set; }
            public decimal? PrecioM2 { get; set; }
        }

        // Resultado sin clave de SqlMercadoAmbientes: una fila por operacion, tipo y ambientes (1 a 5)
        private sealed class MercadoAmbientesFila
        {
            public string Slug { get; set; } = string.Empty;
            public string Operacion { get; set; } = string.Empty;
            public string Tipo { get; set; } = string.Empty;
            public int Ambientes { get; set; }
            public decimal? Promedio { get; set; }
            public int? Propiedades { get; set; }
            public decimal? Expensas { get; set; }
            public decimal? PrecioM2 { get; set; }
        }
    }
}
