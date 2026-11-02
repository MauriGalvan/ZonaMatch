using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Builds the GeoJSON inside PostGIS (ST_AsGeoJSON + json_agg): geometries are never materialized in .NET.
    // SQL is parameterized; only constants are concatenated into it.
    public class MapRepository : IMapRepository
    {
        private const string Schema = ZonaMatchDbOptions.GeoSchema;

        // Around one-meter precision keeps the payload small.
        private const int GeoJsonCoordinatePrecision = 5;

        // Uses the GiST index on geom.
        private const string ViewportPredicate = "t.geom && ST_MakeEnvelope(@minLon, @minLat, @maxLon, @maxLat, 4326)";

        private readonly ZonaMatchDbContext _dbContext;

        public MapRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<string> GetDistrictsAsync(double simplificationTolerance) =>
            ReadAsync(
                FeatureCollection("partidos", "t.key",
                    "json_build_object('key', t.key, 'name', t.nombre, 'province', t.provincia, 'municipalityId', t.municipio_id)",
                    SimplifiedGeometrySql),
                new NpgsqlParameter("tolerance", simplificationTolerance));

        public Task<string> GetCommunesAsync(double simplificationTolerance) =>
            ReadAsync(
                FeatureCollection("comunas", "t.key",
                    "json_build_object('key', t.key, 'name', t.nombre, 'cabaId', t.caba_id)",
                    SimplifiedGeometrySql),
                new NpgsqlParameter("tolerance", simplificationTolerance));

        public Task<int> CountCensusTractsAsync(BoundingBox boundingBox) =>
            CountAsync($"SELECT count(*)::int AS \"Value\" FROM {Schema}.radios t WHERE {ViewportPredicate}",
                GetViewportParameters(boundingBox));

        public Task<string> GetCensusTractsAsync(BoundingBox boundingBox, double simplificationTolerance) =>
            ReadAsync(
                FeatureCollection("radios", "t.id",
                    "json_build_object('id', t.id, 'origin', t.origen, 'department', t.depto, 'commune', t.comuna, 'fraction', t.fraccion, 'tract', t.radio)",
                    SimplifiedGeometrySql,
                    $"WHERE {ViewportPredicate}"),
                GetViewportParameters(boundingBox).Append(new NpgsqlParameter("tolerance", simplificationTolerance)).ToArray());

        public Task<int> CountSchoolsAsync(BoundingBox boundingBox, string? level, string? sector)
        {
            var (where, parameters) = BuildSchoolFilter(boundingBox, level, sector);
            return CountAsync($"SELECT count(*)::int AS \"Value\" FROM {Schema}.escuelas t {where}", parameters);
        }

        public Task<string> GetSchoolsAsync(BoundingBox boundingBox, string? level, string? sector)
        {
            var (where, parameters) = BuildSchoolFilter(boundingBox, level, sector);

            return ReadAsync(
                FeatureCollection("escuelas", "t.clave_natural",
                    "json_build_object('naturalKey', t.clave_natural, 'name', t.nombre, 'level', t.nivel, 'sector', t.sector, 'address', t.direccion, 'locality', t.localidad)",
                    $"ST_AsGeoJSON(t.geom, {GeoJsonCoordinatePrecision})",
                    where),
                parameters);
        }

        private static string SimplifiedGeometrySql =>
            $"ST_AsGeoJSON(ST_SimplifyPreserveTopology(t.geom, @tolerance), {GeoJsonCoordinatePrecision})";

        private static string FeatureCollection(string table, string id, string properties, string geometry, string where = "") =>
            $"""
            SELECT json_build_object(
                'type', 'FeatureCollection',
                'features', COALESCE(json_agg(json_build_object(
                    'type', 'Feature',
                    'id', {id},
                    'properties', {properties},
                    'geometry', {geometry}::json)), '[]'::json))::text AS "Value"
            FROM {Schema}.{table} t
            {where}
            """;

        private static NpgsqlParameter[] GetViewportParameters(BoundingBox boundingBox) =>
        [
            new("minLon", boundingBox.MinLon),
            new("minLat", boundingBox.MinLat),
            new("maxLon", boundingBox.MaxLon),
            new("maxLat", boundingBox.MaxLat)
        ];

        private static (string Where, NpgsqlParameter[] Parameters) BuildSchoolFilter(BoundingBox boundingBox, string? level, string? sector)
        {
            var where = $"WHERE {ViewportPredicate}";
            var parameters = GetViewportParameters(boundingBox).ToList();

            if (!string.IsNullOrWhiteSpace(level))
            {
                where += " AND t.nivel = @level";
                parameters.Add(new NpgsqlParameter("level", level));
            }

            if (!string.IsNullOrWhiteSpace(sector))
            {
                where += " AND t.sector = @sector";
                parameters.Add(new NpgsqlParameter("sector", sector));
            }

            return (where, parameters.ToArray());
        }

        private Task<string> ReadAsync(string sql, params NpgsqlParameter[] parameters) =>
            _dbContext.Database.SqlQueryRaw<string>(sql, parameters).SingleAsync();

        private Task<int> CountAsync(string sql, params NpgsqlParameter[] parameters) =>
            _dbContext.Database.SqlQueryRaw<int>(sql, parameters).SingleAsync();
    }
}
