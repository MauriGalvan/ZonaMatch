using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Repositorio base para entidades geo-localizadas mediante un area (columna "geometry")
    public class GeoPoligonoRepository<T> : Repository<T>, IGeoPoligonoRepository<T> where T : class, IUbicacionPoligono
    {
        // 1 grado de latitud ~ 111.320 metros: aproximacion para el radio de busqueda sobre
        // columnas "geometry" en SRID 4326 (que trabajan en grados, no en metros).
        // Si se necesita precision exacta conviene reproyectar a un SRID metrico
        // (ej. EPSG:5343 para AMBA) o migrar la columna a "geography".
        private const double MetrosPorGrado = 111_320d;

        public GeoPoligonoRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<T?> GetQueContienePuntoAsync(double latitud, double longitud)
        {
            var punto = GeoFactory.CrearPunto(latitud, longitud);

            return await _dbContext.Set<T>()
                .FirstOrDefaultAsync(e => e.Geometria.Contains(punto));
        }

        public async Task<IReadOnlyList<T>> GetCercanosAsync(double latitud, double longitud, double radioMetros)
        {
            var punto = GeoFactory.CrearPunto(latitud, longitud);
            var radioGrados = radioMetros / MetrosPorGrado;

            return await _dbContext.Set<T>()
                .Where(e => e.Geometria.IsWithinDistance(punto, radioGrados))
                .ToListAsync();
        }
    }
}
