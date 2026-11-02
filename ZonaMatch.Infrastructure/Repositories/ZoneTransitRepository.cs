using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Reads zone transit summaries populated from GTFS data by etl/calculate_transit.sql.
    public class ZoneTransitRepository : IZoneTransitRepository
    {
        private const string Schema = ZonaMatchDbOptions.GeoSchema;

        private static readonly string StopsSql = $"""
            SELECT p.modo AS "Mode", p.paradas AS "StopCount", p.lineas AS "LineCount"
            FROM {Schema}.zonas z
            JOIN {Schema}.zona_transporte_paradas p USING (osm_id)
            WHERE z.elegida AND z.slug = @slug
            """;

        private static readonly string DeparturesSql = $"""
            SELECT l.modo AS "Mode", l.linea AS "Line", l.franja AS "TimeWindow", l.salidas AS "DepartureCount",
                   l.paradas AS "StopCount"
            FROM {Schema}.zonas z
            JOIN {Schema}.zona_transporte_lineas l USING (osm_id)
            WHERE z.elegida AND z.slug = @slug
            """;

        private readonly ZonaMatchDbContext _dbContext;
        private readonly ILogger<ZoneTransitRepository> _logger;

        public ZoneTransitRepository(ZonaMatchDbContext dbContext, ILogger<ZoneTransitRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public Task<IReadOnlyList<ZoneTransitModeStops>> ListStopsAsync(string slug, CancellationToken cancellationToken = default) =>
            ListAsync<ZoneTransitModeStops>(StopsSql, slug, cancellationToken);

        public Task<IReadOnlyList<TransitLineDepartures>> ListDeparturesAsync(string slug, CancellationToken cancellationToken = default) =>
            ListAsync<TransitLineDepartures>(DeparturesSql, slug, cancellationToken);

        private async Task<IReadOnlyList<T>> ListAsync<T>(string sql, string slug, CancellationToken cancellationToken)
        {
            try
            {
                return await _dbContext.Database
                    .SqlQueryRaw<T>(sql, new NpgsqlParameter("slug", slug))
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                // Transit tables may be missing when the zone data import has not been run.
                _logger.LogWarning("Zone transit tables are missing. Run ZONE-DATA-DOCKER.bat. ({Message})", ex.MessageText);
                return [];
            }
        }
    }
}
