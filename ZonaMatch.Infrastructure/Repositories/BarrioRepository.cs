using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class BarrioRepository : GeoPoligonoRepository<Barrio>, IBarrioRepository
    {
        public BarrioRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }
    }
}
