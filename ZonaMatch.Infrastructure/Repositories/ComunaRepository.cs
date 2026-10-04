using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ComunaRepository : GeoPoligonoRepository<Comuna>, IComunaRepository
    {
        public ComunaRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }
    }
}
