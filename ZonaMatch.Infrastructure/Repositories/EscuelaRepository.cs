using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class EscuelaRepository : GeoPuntoRepository<Escuela>, IEscuelaRepository
    {
        public EscuelaRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }

        public Task<IReadOnlyList<(Escuela Entidad, double DistanciaMetros)>> GetCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel, string? sector)
        {
            IQueryable<Escuela> query = _dbContext.Set<Escuela>();

            if (!string.IsNullOrWhiteSpace(nivel))
                query = query.Where(e => e.Nivel == nivel);

            if (!string.IsNullOrWhiteSpace(sector))
                query = query.Where(e => e.Sector == sector);

            return BuscarEnRadioAsync(query, latitud, longitud, radioMetros);
        }
    }
}
