using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZonaMatch.Application.DTOs.PointOfInterest.Responses;
using ZonaMatch.Application.DTOs.ZoneRecommendation;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.PointsOfInterest;
using ZonaMatch.Domain.Common;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Points of interest read from the osm2pgsql import (schema "osm", default style, SRID 3857).
    // Nodes and polygons are both used; a polygon is represented by a point inside it (ST_PointOnSurface).
    // The OSM tags -> app category mapping lives in the CASE below.
    public class PointOfInterestRepository : IPointOfInterestRepository
    {
        private const string Schema = ZonaMatchDbOptions.OsmSchema;

        // First matching rule wins (for example, a pharmacy is classified as shopping rather than healthcare).
        private const string ClassificationSql = $"""
            CASE
                WHEN railway IN ('station', 'halt', 'tram_stop', 'subway_entrance')
                     OR public_transport = 'station'
                     OR amenity = 'bus_station'
                     OR highway = 'bus_stop' THEN '{PointOfInterestCategory.PublicTransport}'
                WHEN amenity IN ('kindergarten', 'school', 'college', 'university') THEN '{PointOfInterestCategory.Education}'
                WHEN amenity IN ('hospital', 'clinic', 'doctors', 'dentist') THEN '{PointOfInterestCategory.Healthcare}'
                WHEN shop IS NOT NULL
                     OR amenity IN ('pharmacy', 'bank', 'marketplace', 'post_office') THEN '{PointOfInterestCategory.Shopping}'
                WHEN leisure IN ('sports_centre', 'fitness_centre', 'stadium', 'swimming_pool') THEN '{PointOfInterestCategory.Sports}'
                WHEN leisure IN ('park', 'garden', 'nature_reserve') THEN '{PointOfInterestCategory.GreenSpaces}'
                WHEN amenity IN ('restaurant', 'cafe', 'bar', 'pub', 'fast_food', 'ice_cream',
                                 'theatre', 'cinema', 'arts_centre', 'library')
                     OR tourism IN ('museum', 'gallery') THEN '{PointOfInterestCategory.Culture}'
            END
            """;

        // Value of the tag that classified the point, same priority as above
        private const string TypeSql = """
            CASE
                WHEN railway IN ('station', 'halt', 'tram_stop', 'subway_entrance') THEN railway
                WHEN highway = 'bus_stop' THEN highway
                WHEN amenity IS NOT NULL THEN amenity
                WHEN shop IS NOT NULL THEN shop
                WHEN public_transport IS NOT NULL THEN public_transport
                ELSE COALESCE(leisure, tourism)
            END
            """;

        private const string OsmTagColumns = "name, amenity, shop, leisure, tourism, railway, public_transport, highway, operator";

        private const string App = ZonaMatchDbOptions.AppSchema;

        // Classified points inside an area and their distance from a reference point. Shared by the list and summary
        // queries for a radius or zone. The origin CTE provides a 3857 filter geometry and a geography reference;
        // candidates pass the index-friendly bounding filter and then the exact filter on the 4326 point location.
        // The final CTE contains points that meet the selected area and category.
        // The approved community contributions, persisted in app.community_pois, are applied over OSM: the points
        // added by users ("a" + contribution id) join the candidates, and the changes to an OSM point replace its
        // name, category or location, or hide it if it closed.
        private static string BuildPointsInAreaSql(string originCte, Func<string, string> boundingFilter, string exactFilter) => $"""
            WITH {originCte},
            candidates AS (
                SELECT 'n' || osm_point.osm_id AS id, {OsmTagColumns}, osm_point.way AS location
                FROM {Schema}.planet_osm_point osm_point, origin
                WHERE {boundingFilter("osm_point.way")}
                UNION ALL
                SELECT CASE WHEN osm_polygon.osm_id < 0 THEN 'r' || -osm_polygon.osm_id ELSE 'w' || osm_polygon.osm_id END, {OsmTagColumns},
                       ST_PointOnSurface(osm_polygon.way)
                FROM {Schema}.planet_osm_polygon osm_polygon, origin
                WHERE {boundingFilter("osm_polygon.way")}
            ),
            classified_points AS (
                SELECT id, name, operator, {ClassificationSql} AS category, {TypeSql} AS point_type,
                       ST_Transform(location, 4326) AS location, 'osm' AS data_source
                FROM candidates
                WHERE name IS NOT NULL OR highway = 'bus_stop' OR railway = 'subway_entrance'
                UNION ALL
                SELECT community_point.id, community_point.name, NULL, community_point.category, NULL,
                       community_point.location, 'vecinos'
                FROM {App}.community_pois community_point
                WHERE community_point.is_new AND NOT community_point.removed
            ),
            corrected_points AS (
                SELECT classified.id,
                       COALESCE(community.name, classified.name) AS name,
                       classified.operator,
                       COALESCE(community.category, classified.category) AS category,
                       -- the OSM type no longer describes a place moved to another category
                       CASE WHEN community.category IS NULL OR community.is_new THEN classified.point_type END AS point_type,
                       COALESCE(community.location, classified.location) AS location,
                       classified.data_source
                FROM classified_points classified
                LEFT JOIN {App}.community_pois community ON community.id = classified.id
                WHERE community.removed IS NOT TRUE
            ),
            points_in_area AS (
                SELECT poi.*, ST_Distance(poi.location::geography, origin.geography) AS distance_meters
                FROM corrected_points poi, origin
                WHERE poi.category IS NOT NULL
                  AND {exactFilter}
            )
            """;

        // Web Mercator meters are stretched by 1/cos(lat): the radius is scaled by k for the ST_DWithin prefilter,
        // then the exact distance is measured on the geography
        private static readonly string PointsWithinRadiusSql = BuildPointsInAreaSql(
            """
            origin AS (
                SELECT ST_Transform(ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326), 3857) AS geometry_3857,
                       ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326)::geography AS geography,
                       1 / cos(radians(@latitude)) AS latitude_scale
            )
            """,
            geometry => $"ST_DWithin({geometry}, origin.geometry_3857, @radius_meters * origin.latitude_scale)",
            "ST_DWithin(poi.location::geography, origin.geography, @radius_meters)");

        // The zone of the @slug parameter (same choice as GET /api/v1/zones/{slug}); distances are measured from its center.
        // A polygon counts when the point that represents it falls inside the zone.
        private static readonly string PointsWithinZoneSql = BuildPointsInAreaSql(
            $"""
            {ZoneSql.ZoneBySlug},
            origin AS (
                SELECT zone.way AS geometry_3857, zone.geom AS geometry_4326,
                       ({ZoneSql.GetCenter("zone.geom")})::geography AS geography
                FROM selected_zone zone
            )
            """,
            geometry => $"ST_Intersects({geometry}, origin.geometry_3857)",
            "ST_Intersects(poi.location, origin.geometry_4326)");

        private static string BuildNearbyPointsSql(string pointsQuery) => $"""
            {pointsQuery}
            SELECT id AS "Id",
                   name AS "Name",
                   category AS "Category",
                   point_type AS "Type",
                   operator AS "Operator",
                   ST_Y(location) AS "Latitude",
                   ST_X(location) AS "Longitude",
                   distance_meters AS "DistanceMeters",
                   data_source AS "Source"
            FROM points_in_area
            WHERE category = ANY(@categories)
            ORDER BY distance_meters
            LIMIT @limit
            """;

        private static string BuildPointSummarySql(string pointsQuery) => $"""
            {pointsQuery}
            SELECT category AS "Category", point_type AS "Type", count(*)::int AS "Count"
            FROM points_in_area
            GROUP BY category, point_type
            """;

        private static readonly string NearbyPointsSql = BuildNearbyPointsSql(PointsWithinRadiusSql);
        private static readonly string PointSummarySql = BuildPointSummarySql(PointsWithinRadiusSql);
        private static readonly string PointsInZoneSql = BuildNearbyPointsSql(PointsWithinZoneSql);
        private static readonly string PointsInZoneSummarySql = BuildPointSummarySql(PointsWithinZoneSql);

        private static readonly string ProfileRecommendationsSql = $"""
            WITH {ZoneSql.ZoneBySlug},
            zone_area AS (
                SELECT z.way AS geometry_3857, z.geom AS geometry_4326,
                       -- Distances from the point chosen on the map, or from the zone center without one
                       COALESCE(ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326), {ZoneSql.GetCenter("z.geom")})::geography AS center
                FROM selected_zone z
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

        public PointOfInterestRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<IReadOnlyList<PointOfInterestResponse>> GetNearbyAsync(
            double latitude, double longitude, double radiusMeters, IReadOnlyCollection<string> categories, int limit,
            CancellationToken cancellationToken = default) =>
            ListAsync(NearbyPointsSql, cancellationToken,
                new NpgsqlParameter("latitude", latitude),
                new NpgsqlParameter("longitude", longitude),
                new NpgsqlParameter("radius_meters", radiusMeters),
                new NpgsqlParameter("categories", categories.ToArray()),
                new NpgsqlParameter("limit", limit));

        public Task<IReadOnlyList<PointOfInterestCount>> CountByTypeAsync(
            double latitude, double longitude, double radiusMeters, CancellationToken cancellationToken = default) =>
            CountAsync(PointSummarySql, cancellationToken,
                new NpgsqlParameter("latitude", latitude),
                new NpgsqlParameter("longitude", longitude),
                new NpgsqlParameter("radius_meters", radiusMeters));

        public Task<IReadOnlyList<PointOfInterestResponse>> GetInZoneAsync(
            string slug, IReadOnlyCollection<string> categories, int limit, CancellationToken cancellationToken = default) =>
            ListAsync(PointsInZoneSql, cancellationToken,
                new NpgsqlParameter("slug", slug),
                new NpgsqlParameter("categories", categories.ToArray()),
                new NpgsqlParameter("limit", limit));

        public Task<IReadOnlyList<PointOfInterestCount>> CountByTypeInZoneAsync(
            string slug, CancellationToken cancellationToken = default) =>
            CountAsync(PointsInZoneSummarySql, cancellationToken, new NpgsqlParameter("slug", slug));

        public async Task<IReadOnlyList<ProfileRecommendationCandidate>> GetProfileRecommendationsInZoneAsync(
            string slug,
            IReadOnlyCollection<string> categories,
            int limitPerCategory,
            (double Latitude, double Longitude)? origin = null,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Database
                .SqlQueryRaw<ProfileRecommendationCandidate>(
                    ProfileRecommendationsSql,
                    new NpgsqlParameter("slug", slug),
                    new NpgsqlParameter("categories", categories.ToArray()),
                    new NpgsqlParameter("limit", limitPerCategory),
                    new NpgsqlParameter("latitude", NpgsqlDbType.Double) { Value = (object?)origin?.Latitude ?? DBNull.Value },
                    new NpgsqlParameter("longitude", NpgsqlDbType.Double) { Value = (object?)origin?.Longitude ?? DBNull.Value })
                .ToListAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<PointOfInterestResponse>> ListAsync(
            string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<PointOfInterestRow>(sql, parameters)
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new PointOfInterestResponse(
                    row.Id, row.Name, row.Category, row.Type, row.Operator, row.Latitude, row.Longitude, row.DistanceMeters, row.Source))
                .ToList();
        }

        private async Task<IReadOnlyList<PointOfInterestCount>> CountAsync(
            string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<PointOfInterestCountRow>(sql, parameters)
                .ToListAsync(cancellationToken);

            return rows.Select(row => new PointOfInterestCount(row.Category, row.Type, row.Count)).ToList();
        }

        // Keyless results of the queries above; property names match the column aliases
        private sealed class PointOfInterestCountRow
        {
            public string Category { get; set; } = string.Empty;
            public string? Type { get; set; }
            public int Count { get; set; }
        }

        private sealed class PointOfInterestRow
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public string Category { get; set; } = string.Empty;
            public string? Type { get; set; }
            public string? Operator { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double DistanceMeters { get; set; }
            public string Source { get; set; } = string.Empty;
        }
    }
}
