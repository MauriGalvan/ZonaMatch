using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class CommuneRepository : PolygonRepository<Commune>, ICommuneRepository
    {
        public CommuneRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }
    }
}
