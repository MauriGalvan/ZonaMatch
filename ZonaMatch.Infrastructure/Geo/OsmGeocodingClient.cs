using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.Geolocation;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Geo
{
    // Reverse geocoding against the local osm2pgsql import (schema "osm", default "pgsql" output, SRID 3857).
    // No external calls. Only covers the area that was imported (AMBA, see OSM-DATA-UPDATER-DOCKER.bat).
    //   - place:     smallest named place (amenity/shop/tourism/leisure/office polygon or node) at the point,
    //                or any building with a house number
    //   - street:    nearest named road
    //   - locality:  smallest administrative/place polygon (suburb, town, neighborhood...) containing the point
    // osm2pgsql's default style has no addr:street, so the street is the nearest road, not the tagged one.
    public class OsmGeocodingClient : IGeocodingClient
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;
        // Web Mercator meters are stretched by 1/cos(lat), so metric radii are scaled by k
        private const string ReverseGeocodingSql = $"""
            WITH input_point AS (
                SELECT ST_Transform(ST_SetSRID(ST_MakePoint(@lon, @lat), 4326), 3857) AS geometry,
                       1 / cos(radians(@lat)) AS latitude_scale
            )
            SELECT place.name AS "Name",
                   place.house_number AS "StreetNumber",
                   street.name AS "Street",
                   locality.name AS "Locality"
            FROM input_point point
            LEFT JOIN LATERAL (
                SELECT feature.name, feature."addr:housenumber" AS house_number
                FROM (
                    SELECT name, "addr:housenumber", way, way_area AS feature_area, amenity, shop, tourism, leisure, office
                    FROM {Schema}.planet_osm_polygon
                    UNION ALL
                    SELECT name, "addr:housenumber", way, 0::real, amenity, shop, tourism, leisure, office
                    FROM {Schema}.planet_osm_point
                ) feature
                WHERE ST_DWithin(feature.way, point.geometry, 30 * point.latitude_scale)
                  AND ((feature.name IS NOT NULL
                        AND (feature.amenity IS NOT NULL OR feature.shop IS NOT NULL OR feature.tourism IS NOT NULL
                             OR feature.leisure IS NOT NULL OR feature.office IS NOT NULL))
                       OR feature."addr:housenumber" IS NOT NULL)
                ORDER BY ST_Distance(feature.way, point.geometry), feature.feature_area
                LIMIT 1
            ) place ON true
            LEFT JOIN LATERAL (
                SELECT road.name
                FROM {Schema}.planet_osm_line road
                WHERE road.highway IS NOT NULL
                  AND road.highway NOT IN ('footway', 'path', 'steps', 'cycleway', 'track')
                  AND road.name IS NOT NULL
                  AND ST_DWithin(road.way, point.geometry, 100 * point.latitude_scale)
                ORDER BY ST_Distance(road.way, point.geometry)
                LIMIT 1
            ) street ON true
            LEFT JOIN LATERAL (
                SELECT area.name
                FROM {Schema}.planet_osm_polygon area
                WHERE area.name IS NOT NULL
                  AND ST_Intersects(area.way, point.geometry)
                  AND (area.place IN ('suburb', 'town', 'village', 'city', 'neighbourhood', 'locality')
                       OR (area.boundary = 'administrative' AND area.admin_level IN ('8', '9', '10')))
                ORDER BY area.way_area
                LIMIT 1
            ) locality ON true
            """;

        private readonly ZonaMatchDbContext _dbContext;

        public OsmGeocodingClient(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GeocodedAddress?> ReverseAsync(
            double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            var row = await _dbContext.Database
                .SqlQueryRaw<OsmAddressRow>(ReverseGeocodingSql, new NpgsqlParameter("lat", latitude), new NpgsqlParameter("lon", longitude))
                .SingleAsync(cancellationToken);

            if (row.Name is null && row.Street is null && row.StreetNumber is null && row.Locality is null)
                return null;

            var streetAddress = string.Join(' ', new[] { row.Street, row.StreetNumber }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var fullAddress = string.Join(", ",
                new[] { row.Name, streetAddress, row.Locality }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return new GeocodedAddress(
                Name: row.Name,
                Street: row.Street,
                StreetNumber: row.StreetNumber,
                Locality: row.Locality,
                Province: null,
                PostalCode: null,
                FullAddress: fullAddress.Length > 0 ? fullAddress : null);
        }

        // Keyless result of the query above; property names match the column aliases.
        private sealed class OsmAddressRow
        {
            public string? Name { get; set; }
            public string? StreetNumber { get; set; }
            public string? Street { get; set; }
            public string? Locality { get; set; }
        }
    }
}
