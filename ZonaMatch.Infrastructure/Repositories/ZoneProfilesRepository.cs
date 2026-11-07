using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Builds profiles for every selected zone in geo.zonas for the scoring engine: census and crime data
    // (geo.zona_indicadores), transit (geo.zona_transporte_*), OSM amenities, and housing market data
    // (app.zone_market). Counting amenities across all zones takes a few seconds, so results are cached in
    // ZoneProfilesCache. The data changes only when the ETL is run again.
    public class ZoneProfilesRepository : IZoneProfilesRepository
    {
        private const string Geo = ZonaMatchDbOptions.GeoSchema;
        private const string Osm = ZonaMatchDbOptions.OsmSchema;
        private const string App = ZonaMatchDbOptions.AppSchema;

        // Uses the same categories as PointOfInterestRepository (education, health, green spaces).
        // Polygons are counted by an interior point. Green area includes parks inside the zone in real square
        // meters (the area in SRID 3857 is adjusted by cos^2 of the latitude).
        private static readonly string ZoneProfilesSql = $"""
            WITH zones AS (
                SELECT DISTINCT ON (z.slug) z.osm_id, z.slug, z.nombre AS name, z.nivel AS level,
                       z.superficie_km2 AS area_km2, z.geom,
                       ST_Transform(z.geom, 3857) AS way
                FROM {Geo}.zonas z
                WHERE z.elegida
                  -- This OSM locality is a duplicate representation of all CABA; other same-area
                  -- zones (for example neighborhoods matching their comuna) are valid ranking zones.
                  AND z.slug <> 'buenos-aires'
                ORDER BY z.slug, z.nivel DESC
            ),
            tagged_features AS (
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
            features AS (
                SELECT CASE
                           WHEN amenity IN ('kindergarten', 'school', 'college', 'university') THEN 'education'
                           WHEN amenity IN ('hospital', 'clinic', 'doctors') THEN 'health'
                           ELSE 'green'
                       END AS category, way
                FROM tagged_features
            ),
            amenities AS (
                SELECT z.osm_id,
                       count(*) FILTER (WHERE p.category = 'education')::int AS school_count,
                       count(*) FILTER (WHERE p.category = 'health')::int AS health_center_count,
                       count(*) FILTER (WHERE p.category = 'green')::int AS green_space_count
                FROM zones z
                LEFT JOIN features p ON ST_Intersects(p.way, z.way)
                GROUP BY z.osm_id
            ),
            green_areas AS (
                SELECT z.osm_id,
                       COALESCE(sum(ST_Area(ST_Intersection(ST_MakeValid(green_feature.way), z.way))
                                    * cos(radians(ST_Y(ST_Centroid(z.geom)))) ^ 2), 0) AS area_m2
                FROM zones z
                LEFT JOIN {Osm}.planet_osm_polygon green_feature
                       ON green_feature.leisure IN ('park', 'garden', 'nature_reserve') AND ST_Intersects(green_feature.way, z.way)
                GROUP BY z.osm_id
            ),
            transit_line_counts AS (
                SELECT osm_id, sum(lineas)::int AS line_count FROM {Geo}.zona_transporte_paradas GROUP BY osm_id
            ),
            peak_hour_departures AS (
                SELECT osm_id, sum(salidas)::int AS departure_count FROM {Geo}.zona_transporte_lineas
                WHERE franja = 'manana' GROUP BY osm_id
            )
            SELECT z.slug AS "Slug", z.name AS "Name", z.level AS "Level", z.area_km2 AS "AreaKm2",
                   COALESCE(i.poblacion, 0) AS "Population", i.delitos_comparables AS "CrimeCount",
                   COALESCE(transit.line_count, 0) AS "TransitLineCount", COALESCE(peak.departure_count, 0) AS "PeakHourDepartures",
                   amenities.school_count AS "SchoolCount", amenities.health_center_count AS "HealthCenterCount", amenities.green_space_count AS "GreenSpaceCount",
                   green_areas.area_m2 AS "GreenSpaceAreaM2",
                   ST_Y({ZoneSql.GetCenter("z.geom")}) AS "Latitude", ST_X({ZoneSql.GetCenter("z.geom")}) AS "Longitude"
            FROM zones z
            LEFT JOIN {Geo}.zona_indicadores i USING (osm_id)
            LEFT JOIN transit_line_counts transit USING (osm_id)
            LEFT JOIN peak_hour_departures peak USING (osm_id)
            JOIN amenities USING (osm_id)
            JOIN green_areas USING (osm_id)
            """;

        // Rent and sale data for each zone (app.zone_market), by operation and property type ("all" combines all types):
        // rent and expenses are in ARS; sale prices are in USD. Districts and communes without a row for a combination
        // aggregate their contained zones using a weighted average based on published listings.
        private static readonly string HousingMarketSql = $"""
            WITH zones AS (
                SELECT DISTINCT ON (z.slug) z.slug, z.nivel AS level, z.geom
                FROM {Geo}.zonas z
                WHERE z.elegida AND z.slug <> 'buenos-aires'
                ORDER BY z.slug, z.nivel DESC
            ),
            market_rows AS (
                SELECT zone_slug, operation, property_type, average_price, average_expenses_ars, average_price_m2, listings,
                       price_deciles, price_m2_deciles, price_shares, price_m2_shares
                FROM {App}.zone_market
                WHERE average_price IS NOT NULL
            ),
            zone_market_rows AS (
                SELECT z.slug, market.operation, market.property_type, market.average_price,
                       market.listings AS listing_count, market.average_expenses_ars AS average_expenses,
                       market.average_price_m2 AS average_price_per_m2,
                       market.price_deciles, market.price_m2_deciles, market.price_shares, market.price_m2_shares
                FROM zones z
                JOIN market_rows market ON market.zone_slug = z.slug
            ),
            aggregated_market_rows AS (
                SELECT z.slug, market.operation, market.property_type,
                       round(sum(market.average_price * greatest(market.listings, 1)) / sum(greatest(market.listings, 1))) AS average_price,
                       sum(market.listings)::int AS listing_count,
                       round(sum(market.average_expenses_ars * greatest(market.listings, 1))
                             / nullif(sum(greatest(market.listings, 1)) FILTER (WHERE market.average_expenses_ars IS NOT NULL), 0)) AS average_expenses,
                       round(sum(market.average_price_m2 * greatest(market.listings, 1))
                             / nullif(sum(greatest(market.listings, 1)) FILTER (WHERE market.average_price_m2 IS NOT NULL), 0)) AS average_price_per_m2,
                       NULL::numeric[] AS price_deciles, NULL::numeric[] AS price_m2_deciles,
                       NULL::numeric[] AS price_shares, NULL::numeric[] AS price_m2_shares
                FROM zones z
                JOIN zones contained ON contained.level > 5 AND ST_Contains(z.geom, ST_PointOnSurface(contained.geom))
                JOIN market_rows market ON market.zone_slug = contained.slug
                WHERE z.level <= 5
                  AND NOT EXISTS (SELECT 1 FROM zone_market_rows own
                                  WHERE own.slug = z.slug AND own.operation = market.operation AND own.property_type = market.property_type)
                GROUP BY z.slug, market.operation, market.property_type
            )
            SELECT slug AS "Slug", operation AS "Operation", property_type AS "PropertyType", average_price AS "AveragePrice",
                   listing_count AS "ListingCount", average_expenses AS "AverageExpenses", average_price_per_m2 AS "AveragePricePerM2",
                   price_deciles AS "PriceDeciles", price_m2_deciles AS "PricePerM2Deciles",
                   price_shares AS "PriceShares", price_m2_shares AS "PricePerM2Shares"
            FROM zone_market_rows
            UNION ALL
            SELECT slug, operation, property_type, average_price, listing_count, average_expenses, average_price_per_m2,
                   price_deciles, price_m2_deciles, price_shares, price_m2_shares
            FROM aggregated_market_rows
            """;

        // Prices by number of rooms (app.zone_market_rooms), only the groups with a large enough sample. Inherited zones,
        // districts and communes are already calculated by the ETL.
        private static readonly string RoomsMarketSql = $"""
            SELECT zone_slug AS "Slug", operation AS "Operation", property_type AS "PropertyType", rooms AS "Rooms",
                   average_price AS "AveragePrice", listings AS "ListingCount", average_expenses_ars AS "AverageExpenses",
                   average_price_m2 AS "AveragePricePerM2", price_deciles AS "PriceDeciles", price_m2_deciles AS "PricePerM2Deciles",
                   price_shares AS "PriceShares", price_m2_shares AS "PricePerM2Shares"
            FROM {App}.zone_market_rooms
            WHERE average_price IS NOT NULL AND sample_size >= {ZoneMarketRooms.MinSampleSize}
            """;

        private static readonly string NearbyZonesSql = $"""
            WITH valid_zones AS (
                -- Buenos Aires repeats the full CABA boundary; keep other same-area neighborhoods.
                SELECT z.*
                FROM {Geo}.zonas z
                WHERE z.elegida
                  AND z.slug <> 'buenos-aires'
            ),
            points AS (
                SELECT p.ordinal, ST_SetSRID(ST_MakePoint(p.longitude, p.latitude), 4326) AS geom
                FROM unnest(@latitudes::double precision[], @longitudes::double precision[])
                     WITH ORDINALITY AS p(latitude, longitude, ordinal)
            ),
            containing_zones AS (
                SELECT p.ordinal, z.osm_id, z.slug, z.nivel AS level, z.geom
                FROM points p
                JOIN valid_zones z ON ST_Covers(z.geom, p.geom)
            ),
            most_specific_zones AS (
                SELECT containing.*
                FROM containing_zones containing
                WHERE containing.level = (
                    SELECT max(candidate.level)
                    FROM containing_zones candidate
                    WHERE candidate.ordinal = containing.ordinal
                )
            ),
            segments AS (
                SELECT first_point.ordinal AS origin, second_point.ordinal AS destination,
                       ST_MakeLine(first_point.geom, second_point.geom) AS geom,
                       greatest(first_zone.level, second_zone.level) AS level
                FROM points first_point
                JOIN points second_point ON first_point.ordinal < second_point.ordinal
                JOIN most_specific_zones first_zone ON first_zone.ordinal = first_point.ordinal
                JOIN most_specific_zones second_zone ON second_zone.ordinal = second_point.ordinal
            )
            SELECT DISTINCT candidates.slug AS "Value"
            FROM (
                SELECT neighbor.slug
                FROM most_specific_zones base
                JOIN valid_zones neighbor
                  ON neighbor.nivel = base.level
                 AND ST_Intersects(neighbor.geom, base.geom)

                UNION

                SELECT intermediate.slug
                FROM segments segment
                JOIN valid_zones intermediate
                  ON intermediate.nivel = segment.level
                 AND ST_Intersects(intermediate.geom, segment.geom)
            ) candidates
            """;

        private readonly ZonaMatchDbContext _dbContext;
        private readonly ZoneProfilesCache _cache;
        private readonly ILogger<ZoneProfilesRepository> _logger;

        public ZoneProfilesRepository(ZonaMatchDbContext dbContext, ZoneProfilesCache cache, ILogger<ZoneProfilesRepository> logger)
        {
            _dbContext = dbContext;
            _cache = cache;
            _logger = logger;
        }

        public Task<IReadOnlyList<ZoneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            _cache.GetAsync(LoadZoneProfilesAsync, cancellationToken);

        public Task InvalidateAsync(CancellationToken cancellationToken = default)
        {
            _cache.Invalidate();
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<string>> ListNearbyZoneSlugsAsync(
            IReadOnlyCollection<(double Latitude, double Longitude)> locations,
            CancellationToken cancellationToken = default)
        {
            if (locations.Count == 0)
                return [];

            var latitudes = locations.Select(point => point.Latitude).ToArray();
            var longitudes = locations.Select(point => point.Longitude).ToArray();

            try
            {
                return await _dbContext.Database
                    .SqlQueryRaw<string>(
                        NearbyZonesSql,
                        new NpgsqlParameter("latitudes", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = latitudes },
                        new NpgsqlParameter("longitudes", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = longitudes })
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning("Zone tables are missing. Run ZONE-DATA-DOCKER.bat. ({Message})", ex.MessageText);
                return [];
            }
        }

        private async Task<IReadOnlyList<ZoneProfile>> LoadZoneProfilesAsync(CancellationToken cancellationToken)
        {
            try
            {
                // This query covers the entire AMBA and takes longer than the default command timeout.
                _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));

                var zoneRows = await _dbContext.Database
                    .SqlQueryRaw<ZoneProfileRow>(ZoneProfilesSql)
                    .ToListAsync(cancellationToken);
                var marketRows = (await _dbContext.Database
                        .SqlQueryRaw<HousingMarketRow>(HousingMarketSql)
                        .ToListAsync(cancellationToken))
                    .ToLookup(row => row.Slug, StringComparer.Ordinal);
                var roomsRows = (await ReadRoomsMarketAsync(cancellationToken))
                    .ToLookup(row => row.Slug, StringComparer.Ordinal);

                return zoneRows.Select(row => row.ToZoneProfile(marketRows[row.Slug], roomsRows[row.Slug])).ToList();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning("Zone scoring tables are missing. Run ZONE-DATA-DOCKER.bat. ({Message})", ex.MessageText);
                return [];
            }
        }

        // Without the rooms table (migration AddZoneMarketByRooms not applied) scoring keeps the averages of every size.
        private async Task<IReadOnlyList<RoomsMarketRow>> ReadRoomsMarketAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _dbContext.Database
                    .SqlQueryRaw<RoomsMarketRow>(RoomsMarketSql)
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _logger.LogWarning("app.zone_market_rooms is missing. Apply the migrations. ({Message})", ex.MessageText);
                return [];
            }
        }

        // Keyless result type for the zone profile query; property names match the SQL aliases.
        private sealed class ZoneProfileRow
        {
            public string Slug { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Level { get; set; }
            public double AreaKm2 { get; set; }
            public int Population { get; set; }
            public int? CrimeCount { get; set; }
            public int TransitLineCount { get; set; }
            public int PeakHourDepartures { get; set; }
            public int SchoolCount { get; set; }
            public int HealthCenterCount { get; set; }
            public int GreenSpaceCount { get; set; }
            public double GreenSpaceAreaM2 { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }

            public ZoneProfile ToZoneProfile(IEnumerable<HousingMarketRow> marketRows, IEnumerable<RoomsMarketRow> roomsRows)
            {
                var marketByOperationAndType = marketRows.ToDictionary(row => (row.Operation, row.PropertyType));
                var marketByRooms = roomsRows
                    .GroupBy(row => (row.PropertyType, row.Rooms))
                    .ToDictionary(
                        group => group.Key,
                        group =>
                        {
                            var rent = group.FirstOrDefault(row => row.Operation == "rent");
                            var sale = group.FirstOrDefault(row => row.Operation == "sale");
                            return new HousingMarket(
                                rent?.AveragePrice, rent?.ListingCount, rent?.AverageExpenses, sale?.AveragePrice, sale?.ListingCount,
                                rent?.AveragePricePerM2, sale?.AveragePricePerM2,
                                RentDeciles: rent?.PriceDeciles, RentPerM2Deciles: rent?.PricePerM2Deciles,
                                SaleDecilesUsd: sale?.PriceDeciles, SalePerM2DecilesUsd: sale?.PricePerM2Deciles,
                        RentShares: rent?.PriceShares, RentPerM2Shares: rent?.PricePerM2Shares,
                        SaleShares: sale?.PriceShares, SalePerM2Shares: sale?.PricePerM2Shares);
                        });

                HousingMarket CreateHousingMarket(string propertyType)
                {
                    var rent = marketByOperationAndType.GetValueOrDefault(("rent", propertyType));
                    var sale = marketByOperationAndType.GetValueOrDefault(("sale", propertyType));
                    return new HousingMarket(
                        rent?.AveragePrice, rent?.ListingCount, rent?.AverageExpenses, sale?.AveragePrice, sale?.ListingCount,
                        rent?.AveragePricePerM2, sale?.AveragePricePerM2,
                        RentDeciles: rent?.PriceDeciles, RentPerM2Deciles: rent?.PricePerM2Deciles,
                        SaleDecilesUsd: sale?.PriceDeciles, SalePerM2DecilesUsd: sale?.PricePerM2Deciles,
                        RentShares: rent?.PriceShares, RentPerM2Shares: rent?.PricePerM2Shares,
                        SaleShares: sale?.PriceShares, SalePerM2Shares: sale?.PricePerM2Shares);
                }

                var combinedMarket = CreateHousingMarket(HousingTypes.All);

                return new ZoneProfile
                {
                Slug = Slug,
                Name = Name,
                Level = Level,
                AreaKm2 = AreaKm2,
                Population = Population,
                CrimeCount = CrimeCount,
                TransitLineCount = TransitLineCount,
                PeakHourDepartures = PeakHourDepartures,
                SchoolCount = SchoolCount,
                HealthCenterCount = HealthCenterCount,
                GreenSpaceCount = GreenSpaceCount,
                GreenSpaceAreaM2 = GreenSpaceAreaM2,
                Latitude = Latitude,
                Longitude = Longitude,
                AverageRent = combinedMarket.AverageRent,
                AvailableRentalProperties = combinedMarket.AvailablePropertiesForRent,
                AverageExpenses = combinedMarket.AverageExpenses,
                AverageSalePriceUsd = combinedMarket.AverageSalePriceUsd,
                AvailablePropertiesForSale = combinedMarket.AvailablePropertiesForSale,
                RentPricePerM2 = combinedMarket.RentPricePerM2,
                SalePricePerM2Usd = combinedMarket.SalePricePerM2Usd,
                RentDeciles = combinedMarket.RentDeciles,
                RentPerM2Deciles = combinedMarket.RentPerM2Deciles,
                SaleDecilesUsd = combinedMarket.SaleDecilesUsd,
                SalePerM2DecilesUsd = combinedMarket.SalePerM2DecilesUsd,
                RentShares = combinedMarket.RentShares,
                RentPerM2Shares = combinedMarket.RentPerM2Shares,
                SaleShares = combinedMarket.SaleShares,
                SalePerM2Shares = combinedMarket.SalePerM2Shares,
                MarketByPropertyType = new[] { HousingTypes.Apartment, HousingTypes.House, HousingTypes.Ph }
                    .ToDictionary(propertyType => propertyType, CreateHousingMarket),
                MarketByRooms = marketByRooms,
                };
            }
        }

        // Keyless result type for the market query: operation is rent|sale and property type comes from app.zone_market.
        private sealed class HousingMarketRow
        {
            public string Slug { get; set; } = string.Empty;
            public string Operation { get; set; } = string.Empty;
            public string PropertyType { get; set; } = string.Empty;
            public decimal? AveragePrice { get; set; }
            public int? ListingCount { get; set; }
            public decimal? AverageExpenses { get; set; }
            public decimal? AveragePricePerM2 { get; set; }
            public decimal[]? PriceDeciles { get; set; }
            public decimal[]? PricePerM2Deciles { get; set; }
            public decimal[]? PriceShares { get; set; }
            public decimal[]? PricePerM2Shares { get; set; }
        }

        // Keyless result type for RoomsMarketSql: one row per operation, property type and rooms (1 to 5).
        private sealed class RoomsMarketRow
        {
            public string Slug { get; set; } = string.Empty;
            public string Operation { get; set; } = string.Empty;
            public string PropertyType { get; set; } = string.Empty;
            public int Rooms { get; set; }
            public decimal? AveragePrice { get; set; }
            public int? ListingCount { get; set; }
            public decimal? AverageExpenses { get; set; }
            public decimal? AveragePricePerM2 { get; set; }
            public decimal[]? PriceDeciles { get; set; }
            public decimal[]? PricePerM2Deciles { get; set; }
            public decimal[]? PriceShares { get; set; }
            public decimal[]? PricePerM2Shares { get; set; }
        }
    }
}
