using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class SchoolRepository : PointEntityRepository<School>, ISchoolRepository
    {
        public SchoolRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }

        public Task<IReadOnlyList<(School Entidad, double DistanciaMetros)>> GetCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel, string? sector)
        {
            IQueryable<School> query = _dbContext.Set<School>();

            if (!string.IsNullOrWhiteSpace(nivel))
                query = query.Where(e => e.Nivel == nivel);

            if (!string.IsNullOrWhiteSpace(sector))
                query = query.Where(e => e.Sector == sector);

            return BuscarEnRadioAsync(query, latitud, longitud, radioMetros);
        }
    }
}
