using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Geo
{
    // Reverse geocoding against the local osm2pgsql import (schema "osm", default "pgsql" output, SRID 3857).
    // No external calls. Only covers the area that was imported (AMBA, see OSM-DATA-UPDATER-DOCKER.bat).
    //   - lugar:     smallest named place (amenity/shop/tourism/leisure/office polygon or node) at the point,
    //                or any building with a house number
    //   - calle:     nearest named road
    //   - localidad: smallest administrative/place polygon (suburb, town, barrio...) containing the point
    // osm2pgsql's default style has no addr:street, so the street is the nearest road, not the tagged one.
    public class OsmGeocodingClient : IGeocodingClient
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;

        // Web Mercator meters are stretched by 1/cos(lat), so metric radii are scaled by k
        private const string Sql = $"""
            WITH p AS (
                SELECT ST_Transform(ST_SetSRID(ST_MakePoint(@lon, @lat), 4326), 3857) AS g,
                       1 / cos(radians(@lat)) AS k
            )
            SELECT lugar.name AS "Nombre",
                   lugar.altura AS "Altura",
                   calle.name AS "Calle",
                   loc.name AS "Localidad"
            FROM p
            LEFT JOIN LATERAL (
                SELECT t.name, t."addr:housenumber" AS altura
                FROM (
                    SELECT name, "addr:housenumber", way, way_area AS area, amenity, shop, tourism, leisure, office
                    FROM {Schema}.planet_osm_polygon
                    UNION ALL
                    SELECT name, "addr:housenumber", way, 0::real, amenity, shop, tourism, leisure, office
                    FROM {Schema}.planet_osm_point
                ) t
                WHERE ST_DWithin(t.way, p.g, 30 * p.k)
                  AND ((t.name IS NOT NULL
                        AND (t.amenity IS NOT NULL OR t.shop IS NOT NULL OR t.tourism IS NOT NULL
                             OR t.leisure IS NOT NULL OR t.office IS NOT NULL))
                       OR t."addr:housenumber" IS NOT NULL)
                ORDER BY ST_Distance(t.way, p.g), t.area
                LIMIT 1
            ) lugar ON true
            LEFT JOIN LATERAL (
                SELECT l.name
                FROM {Schema}.planet_osm_line l
                WHERE l.highway IS NOT NULL
                  AND l.highway NOT IN ('footway', 'path', 'steps', 'cycleway', 'track')
                  AND l.name IS NOT NULL
                  AND ST_DWithin(l.way, p.g, 100 * p.k)
                ORDER BY ST_Distance(l.way, p.g)
                LIMIT 1
            ) calle ON true
            LEFT JOIN LATERAL (
                SELECT a.name
                FROM {Schema}.planet_osm_polygon a
                WHERE a.name IS NOT NULL
                  AND ST_Intersects(a.way, p.g)
                  AND (a.place IN ('suburb', 'town', 'village', 'city', 'neighbourhood', 'locality')
                       OR (a.boundary = 'administrative' AND a.admin_level IN ('8', '9', '10')))
                ORDER BY a.way_area
                LIMIT 1
            ) loc ON true
            """;

        private readonly ZonaMatchDbContext _dbContext;

        public OsmGeocodingClient(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<DireccionGeocodificadaDto?> ReversaAsync(
            double latitud, double longitud, CancellationToken cancellationToken = default)
        {
            var fila = await _dbContext.Database
                .SqlQueryRaw<OsmFila>(Sql, new NpgsqlParameter("lat", latitud), new NpgsqlParameter("lon", longitud))
                .SingleAsync(cancellationToken);

            if (fila.Nombre is null && fila.Calle is null && fila.Altura is null && fila.Localidad is null)
                return null;

            var calleYAltura = string.Join(' ', new[] { fila.Calle, fila.Altura }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var completa = string.Join(", ",
                new[] { fila.Nombre, calleYAltura, fila.Localidad }.Where(s => !string.IsNullOrWhiteSpace(s)));

            return new DireccionGeocodificadaDto(
                Nombre: fila.Nombre,
                Calle: fila.Calle,
                Altura: fila.Altura,
                Localidad: fila.Localidad,
                Provincia: null,
                CodigoPostal: null,
                DireccionCompleta: completa.Length > 0 ? completa : null);
        }

        // Keyless result of the query above; property names match the column aliases
        private sealed class OsmFila
        {
            public string? Nombre { get; set; }
            public string? Altura { get; set; }
            public string? Calle { get; set; }
            public string? Localidad { get; set; }
        }
    }
}
