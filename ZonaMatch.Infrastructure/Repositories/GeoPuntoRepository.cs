using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Repositorio base para entidades geo-localizadas mediante un punto (columna "geography")
    public class GeoPuntoRepository<T> : Repository<T>, IGeoPuntoRepository<T> where T : class, IUbicacionPunto
    {
        public GeoPuntoRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetCercanosAsync(
            double latitud, double longitud, double radioMetros)
        {
            var punto = GeoFactory.CrearPunto(latitud, longitud);

            var resultados = await _dbContext.Set<T>()
                .Where(e => e.Ubicacion.IsWithinDistance(punto, radioMetros))
                .Select(e => new { Entidad = e, Distancia = e.Ubicacion.Distance(punto) })
                .OrderBy(x => x.Distancia)
                .ToListAsync();

            return resultados.Select(x => (x.Entidad, x.Distancia)).ToList();
        }

        public async Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetMasCercanosAsync(
            double latitud, double longitud, int cantidad)
        {
            var punto = GeoFactory.CrearPunto(latitud, longitud);

            var resultados = await _dbContext.Set<T>()
                .Select(e => new { Entidad = e, Distancia = e.Ubicacion.Distance(punto) })
                .OrderBy(x => x.Distancia)
                .Take(cantidad)
                .ToListAsync();

            return resultados.Select(x => (x.Entidad, x.Distancia)).ToList();
        }
    }
}
