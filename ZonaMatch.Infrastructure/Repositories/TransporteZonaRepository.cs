using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Lee el resumen de transporte por zona (geo.zona_transporte_paradas y geo.zona_transporte_lineas), que arma
    // etl/calcular_transporte.sql a partir de los GTFS. Solo lectura.
    public class TransporteZonaRepository : ITransporteZonaRepository
    {
        private const string Schema = ZonaMatchDbOptions.GeoSchema;

        private static readonly string SqlParadas = $"""
            SELECT p.modo AS "Modo", p.paradas AS "Paradas", p.lineas AS "Lineas"
            FROM {Schema}.zonas z
            JOIN {Schema}.zona_transporte_paradas p USING (osm_id)
            WHERE z.elegida AND z.slug = @slug
            """;

        private static readonly string SqlSalidas = $"""
            SELECT l.modo AS "Modo", l.linea AS "Linea", l.franja AS "Franja", l.salidas AS "Salidas",
                   l.paradas AS "Paradas"
            FROM {Schema}.zonas z
            JOIN {Schema}.zona_transporte_lineas l USING (osm_id)
            WHERE z.elegida AND z.slug = @slug
            """;

        private readonly ZonaMatchDbContext _dbContext;
        private readonly ILogger<TransporteZonaRepository> _logger;

        public TransporteZonaRepository(ZonaMatchDbContext dbContext, ILogger<TransporteZonaRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public Task<IReadOnlyList<ParadasModoZona>> ListarParadasAsync(string slug, CancellationToken cancellationToken = default) =>
            ListarAsync<ParadasModoZona>(SqlParadas, slug, cancellationToken);

        public Task<IReadOnlyList<SalidasLineaZona>> ListarSalidasAsync(string slug, CancellationToken cancellationToken = default) =>
            ListarAsync<SalidasLineaZona>(SqlSalidas, slug, cancellationToken);

        private async Task<IReadOnlyList<T>> ListarAsync<T>(string sql, string slug, CancellationToken cancellationToken)
        {
            try
            {
                return await _dbContext.Database
                    .SqlQueryRaw<T>(sql, new NpgsqlParameter("slug", slug))
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                // Todavia no se corrio DATOS-ZONAS-DOCKER.bat: la zona queda sin transporte
                _logger.LogWarning("Faltan las tablas de transporte por zona ({Mensaje}). Correr DATOS-ZONAS-DOCKER.bat.", ex.MessageText);
                return [];
            }
        }
    }
}
