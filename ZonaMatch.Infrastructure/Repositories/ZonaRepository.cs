using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ZonaRepository : GeoPoligonoRepository<Zona>, IZonaRepository
    {
        public ZonaRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }
    }
}
