using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Lee geo.zonas y geo.zona_indicadores, que arma etl/calcular_zonas.sql a partir del censo, los delitos y
    // los limites de OSM. Solo lectura: los conteos se convierten en indicadores en IndicadoresZona.
    public class IndicadoresZonaRepository : IIndicadoresZonaRepository
    {
        private const string Schema = ZonaMatchDbOptions.GeoSchema;

        private static readonly string SqlSelect = $"""
            SELECT z.slug AS "Slug", z.nombre AS "Nombre", z.nivel AS "Nivel", z.superficie_km2 AS "SuperficieKm2",
                   i.radios AS "Radios", i.poblacion AS "Poblacion", i.viviendas AS "Viviendas",
                   i.personas_0_14 AS "Personas0a14", i.personas_15_64 AS "Personas15a64",
                   i.personas_65_mas AS "Personas65yMas",
                   i.personas_con_cobertura_salud AS "PersonasConCoberturaSalud",
                   i.personas_con_dato_cobertura AS "PersonasConDatoCobertura",
                   i.personas_universitario AS "PersonasUniversitario",
                   i.personas_con_dato_educacion AS "PersonasConDatoEducacion",
                   i.hogares AS "Hogares", i.hogares_internet AS "HogaresInternet",
                   i.hogares_celular_internet AS "HogaresCelularInternet",
                   i.hogares_computadora AS "HogaresComputadora", i.hogares_nbi AS "HogaresNbi",
                   i.hogares_hacinamiento AS "HogaresHacinamiento", i.hogares_alquilan AS "HogaresAlquilan",
                   i.delitos_anio AS "Delitos", i.delitos_robos AS "Robos", i.delitos_hurtos AS "Hurtos",
                   i.delitos_lesiones AS "Lesiones", i.delitos_homicidios AS "Homicidios",
                   i.delitos_amenazas AS "Amenazas", i.siniestros_viales AS "SiniestrosViales"
            FROM {Schema}.zonas z
            JOIN {Schema}.zona_indicadores i USING (osm_id)
            """;

        private static readonly string SqlPorSlug = $"{SqlSelect} WHERE z.elegida AND z.slug = @slug";

        // La ciudad es la unica zona de nivel 4 del recorte del AMBA
        private static readonly string SqlCaba = $"{SqlSelect} WHERE z.nivel = 4 ORDER BY z.superficie_km2 DESC LIMIT 1";

        private readonly ZonaMatchDbContext _dbContext;
        private readonly ILogger<IndicadoresZonaRepository> _logger;

        public IndicadoresZonaRepository(ZonaMatchDbContext dbContext, ILogger<IndicadoresZonaRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public Task<IndicadoresZona?> ObtenerPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
            PrimeraAsync(SqlPorSlug, cancellationToken, new NpgsqlParameter("slug", slug));

        public Task<IndicadoresZona?> ObtenerCabaAsync(CancellationToken cancellationToken = default) =>
            PrimeraAsync(SqlCaba, cancellationToken);

        private async Task<IndicadoresZona?> PrimeraAsync(
            string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parametros)
        {
            try
            {
                var filas = await _dbContext.Database
                    .SqlQueryRaw<IndicadoresZona>(sql, parametros)
                    .ToListAsync(cancellationToken);

                return filas.FirstOrDefault();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                // Todavia no se corrio DATOS-ZONAS-DOCKER.bat: la zona queda sin indicadores
                _logger.LogWarning("Faltan las tablas de indicadores de zona ({Mensaje}). Correr DATOS-ZONAS-DOCKER.bat.", ex.MessageText);
                return null;
            }
        }
    }
}
