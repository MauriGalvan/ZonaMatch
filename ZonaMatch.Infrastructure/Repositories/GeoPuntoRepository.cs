using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Base repository for entities located by a point (column "geom": geometry(Point,4326), units in degrees)
    public class GeoPuntoRepository<T> : Repository<T>, IGeoPuntoRepository<T> where T : class, IUbicacionPunto
    {
        public GeoPuntoRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }

        public Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetCercanosAsync(
            double latitud, double longitud, double radioMetros) =>
            BuscarEnRadioAsync(_dbContext.Set<T>(), latitud, longitud, radioMetros);

        public async Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetMasCercanosAsync(
            double latitud, double longitud, int cantidad)
        {
            // Equirectangular distance computed in SQL: orders correctly at city scale, unlike raw degrees
            var cosLatitud = Math.Cos(latitud * Math.PI / 180d);

            var candidatos = await _dbContext.Set<T>()
                .OrderBy(e => Math.Pow(e.Ubicacion.Y - latitud, 2) + Math.Pow((e.Ubicacion.X - longitud) * cosLatitud, 2))
                .Take(cantidad)
                .ToListAsync();

            return ConDistancia(candidatos, latitud, longitud);
        }

        // Shared by derived repositories that pre-filter the query (e.g. by nivel) before searching by radius
        protected static async Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> BuscarEnRadioAsync(
            IQueryable<T> query, double latitud, double longitud, double radioMetros)
        {
            var punto = GeoFactory.CrearPunto(latitud, longitud);
            var radioGrados = GeoMath.MetrosAGrados(radioMetros, latitud);

            // ST_DWithin in degrees is a coarse, index-friendly prefilter; exact meters are computed in memory
            var candidatos = await query
                .Where(e => e.Ubicacion.IsWithinDistance(punto, radioGrados))
                .ToListAsync();

            return ConDistancia(candidatos, latitud, longitud)
                .Where(x => x.DistanciaMetros <= radioMetros)
                .ToList();
        }

        private static IReadOnlyList<(T Entidad, double DistanciaMetros)> ConDistancia(
            IEnumerable<T> entidades, double latitud, double longitud) =>
            entidades
                .Select(e => (Entidad: e,
                              DistanciaMetros: GeoMath.DistanciaMetros(latitud, longitud, e.Ubicacion.Y, e.Ubicacion.X)))
                .OrderBy(x => x.DistanciaMetros)
                .ToList();
    }
}
