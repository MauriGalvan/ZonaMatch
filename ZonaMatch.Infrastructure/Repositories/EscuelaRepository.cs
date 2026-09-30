using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class EscuelaRepository : GeoPuntoRepository<Escuela>, IEscuelaRepository
    {
        public EscuelaRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IReadOnlyList<(Escuela Entidad, double DistanciaMetros)>> GetCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel, string? gestion)
        {
            var punto = GeoFactory.CrearPunto(latitud, longitud);

            var query = _dbContext.Set<Escuela>()
                .Where(e => e.Ubicacion.IsWithinDistance(punto, radioMetros));

            if (!string.IsNullOrWhiteSpace(nivel))
                query = query.Where(e => e.Nivel == nivel);

            if (!string.IsNullOrWhiteSpace(gestion))
                query = query.Where(e => e.Gestion == gestion);

            var resultados = await query
                .Select(e => new { Entidad = e, Distancia = e.Ubicacion.Distance(punto) })
                .OrderBy(x => x.Distancia)
                .ToListAsync();

            return resultados.Select(x => (x.Entidad, x.Distancia)).ToList();
        }
    }
}
