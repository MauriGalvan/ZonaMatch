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

        // Web Mercator meters are stretched by 1/cos(lat): the radius is scaled by k for the index-friendly
        // ST_DWithin prefilter, then the exact distance is measured on the geography.
        // Shared by the list and the summary: ends with the "en_radio" CTE (classified points inside the radius).
        private const string PuntosEnRadio = $"""
            WITH p AS (
                SELECT ST_Transform(ST_SetSRID(ST_MakePoint(@lon, @lat), 4326), 3857) AS g,
                       ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography AS geog,
                       1 / cos(radians(@lat)) AS k
            ),
            candidatos AS (
                SELECT 'n' || t.osm_id AS id, {Columnas}, t.way AS punto
                FROM {Schema}.planet_osm_point t, p
                WHERE ST_DWithin(t.way, p.g, @radio * p.k)
                UNION ALL
                SELECT CASE WHEN t.osm_id < 0 THEN 'r' || -t.osm_id ELSE 'w' || t.osm_id END, {Columnas},
                       ST_PointOnSurface(t.way)
                FROM {Schema}.planet_osm_polygon t, p
                WHERE ST_DWithin(t.way, p.g, @radio * p.k)
            ),
            clasificados AS (
                SELECT id, name, operator, {Clasificacion} AS categoria, {Tipo} AS tipo, ST_Transform(punto, 4326) AS punto
                FROM candidatos
                WHERE name IS NOT NULL OR highway = 'bus_stop' OR railway = 'subway_entrance'
            ),
            en_radio AS (
                SELECT c.*, ST_Distance(c.punto::geography, p.geog) AS distancia
                FROM clasificados c, p
                WHERE c.categoria IS NOT NULL
                  AND ST_DWithin(c.punto::geography, p.geog, @radio)
            )
            """;

        private const string SqlCercanos = $"""
            {PuntosEnRadio}
            SELECT id AS "Id",
                   name AS "Nombre",
                   categoria AS "Categoria",
                   tipo AS "Tipo",
                   operator AS "Operador",
                   ST_Y(punto) AS "Latitud",
                   ST_X(punto) AS "Longitud",
                   distancia AS "DistanciaMetros"
            FROM en_radio
            WHERE categoria = ANY(@categorias)
            ORDER BY distancia
            LIMIT @limite
            """;

        private const string SqlResumen = $"""
            {PuntosEnRadio}
            SELECT categoria AS "Categoria", tipo AS "Tipo", count(*)::int AS "Cantidad"
            FROM en_radio
            GROUP BY categoria, tipo
            """;

        private readonly ZonaMatchDbContext _dbContext;

        public PuntoInteresRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<PuntoInteresDto>> GetCercanosAsync(
            double latitud, double longitud, double radioMetros, IReadOnlyCollection<string> categorias, int limite,
            CancellationToken cancellationToken = default)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<PuntoInteresFila>(SqlCercanos,
                    new NpgsqlParameter("lat", latitud),
                    new NpgsqlParameter("lon", longitud),
                    new NpgsqlParameter("radio", radioMetros),
                    new NpgsqlParameter("categorias", categorias.ToArray()),
                    new NpgsqlParameter("limite", limite))
                .ToListAsync(cancellationToken);

            return filas
                .Select(f => new PuntoInteresDto(
                    f.Id, f.Nombre, f.Categoria, f.Tipo, f.Operador, f.Latitud, f.Longitud, f.DistanciaMetros))
                .ToList();
        }

        public async Task<IReadOnlyList<CantidadPorTipo>> ContarPorTipoAsync(
            double latitud, double longitud, double radioMetros, CancellationToken cancellationToken = default)
        {
            var filas = await _dbContext.Database
                .SqlQueryRaw<CantidadFila>(SqlResumen,
                    new NpgsqlParameter("lat", latitud),
                    new NpgsqlParameter("lon", longitud),
                    new NpgsqlParameter("radio", radioMetros))
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
        }
    }
}
