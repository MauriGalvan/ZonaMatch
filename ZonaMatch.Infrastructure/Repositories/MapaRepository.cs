using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Builds the GeoJSON inside PostGIS (ST_AsGeoJSON + json_agg): geometries are never materialized in .NET.
    // SQL is parameterized; only constants are concatenated into it.
    public class MapaRepository : IMapaRepository
    {
        private const string Schema = ZonaMatchDbOptions.GeoSchema;

        // ~1 m precision, keeps the payload small
        private const int Decimales = 5;

        // Uses the GiST index on geom
        private const string EnViewport = "t.geom && ST_MakeEnvelope(@minLon, @minLat, @maxLon, @maxLat, 4326)";

        private readonly ZonaMatchDbContext _dbContext;

        public MapaRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<string> GetPartidosAsync(double tolerancia) =>
            LeerAsync(
                FeatureCollection("partidos", "t.key",
                    "json_build_object('key', t.key, 'nombre', t.nombre, 'provincia', t.provincia, 'municipioId', t.municipio_id)",
                    GeometriaSimplificada),
                new NpgsqlParameter("tol", tolerancia));

        public Task<string> GetComunasAsync(double tolerancia) =>
            LeerAsync(
                FeatureCollection("comunas", "t.key",
                    "json_build_object('key', t.key, 'nombre', t.nombre, 'cabaId', t.caba_id)",
                    GeometriaSimplificada),
                new NpgsqlParameter("tol", tolerancia));

        public Task<int> ContarRadiosAsync(BoundingBox bbox) =>
            ContarAsync($"SELECT count(*)::int AS \"Value\" FROM {Schema}.radios t WHERE {EnViewport}",
                ParametrosViewport(bbox));

        public Task<string> GetRadiosAsync(BoundingBox bbox, double tolerancia) =>
            LeerAsync(
                FeatureCollection("radios", "t.id",
                    "json_build_object('id', t.id, 'origen', t.origen, 'depto', t.depto, 'comuna', t.comuna, 'fraccion', t.fraccion, 'radio', t.radio)",
                    GeometriaSimplificada,
                    $"WHERE {EnViewport}"),
                ParametrosViewport(bbox).Append(new NpgsqlParameter("tol", tolerancia)).ToArray());

        public Task<int> ContarEscuelasAsync(BoundingBox bbox, string? nivel, string? sector)
        {
            var (where, parametros) = FiltroEscuelas(bbox, nivel, sector);
            return ContarAsync($"SELECT count(*)::int AS \"Value\" FROM {Schema}.escuelas t {where}", parametros);
        }

        public Task<string> GetEscuelasAsync(BoundingBox bbox, string? nivel, string? sector)
        {
            var (where, parametros) = FiltroEscuelas(bbox, nivel, sector);

            return LeerAsync(
                FeatureCollection("escuelas", "t.clave_natural",
                    "json_build_object('claveNatural', t.clave_natural, 'nombre', t.nombre, 'nivel', t.nivel, 'sector', t.sector, 'direccion', t.direccion, 'localidad', t.localidad)",
                    $"ST_AsGeoJSON(t.geom, {Decimales})",
                    where),
                parametros);
        }

        private static string GeometriaSimplificada =>
            $"ST_AsGeoJSON(ST_SimplifyPreserveTopology(t.geom, @tol), {Decimales})";

        private static string FeatureCollection(string tabla, string id, string propiedades, string geometria, string where = "") =>
            $"""
            SELECT json_build_object(
                'type', 'FeatureCollection',
                'features', COALESCE(json_agg(json_build_object(
                    'type', 'Feature',
                    'id', {id},
                    'properties', {propiedades},
                    'geometry', {geometria}::json)), '[]'::json))::text AS "Value"
            FROM {Schema}.{tabla} t
            {where}
            """;

        private static NpgsqlParameter[] ParametrosViewport(BoundingBox bbox) =>
        [
            new("minLon", bbox.MinLon),
            new("minLat", bbox.MinLat),
            new("maxLon", bbox.MaxLon),
            new("maxLat", bbox.MaxLat)
        ];

        private static (string Where, NpgsqlParameter[] Parametros) FiltroEscuelas(BoundingBox bbox, string? nivel, string? sector)
        {
            var where = $"WHERE {EnViewport}";
            var parametros = ParametrosViewport(bbox).ToList();

            if (!string.IsNullOrWhiteSpace(nivel))
            {
                where += " AND t.nivel = @nivel";
                parametros.Add(new NpgsqlParameter("nivel", nivel));
            }

            if (!string.IsNullOrWhiteSpace(sector))
            {
                where += " AND t.sector = @sector";
                parametros.Add(new NpgsqlParameter("sector", sector));
            }

            return (where, parametros.ToArray());
        }

        private Task<string> LeerAsync(string sql, params NpgsqlParameter[] parametros) =>
            _dbContext.Database.SqlQueryRaw<string>(sql, parametros).SingleAsync();

        private Task<int> ContarAsync(string sql, params NpgsqlParameter[] parametros) =>
            _dbContext.Database.SqlQueryRaw<int>(sql, parametros).SingleAsync();
    }
}
