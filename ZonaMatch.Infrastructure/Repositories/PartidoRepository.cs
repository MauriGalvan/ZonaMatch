using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class PartidoRepository : GeoPoligonoRepository<Partido>, IPartidoRepository
    {
        public PartidoRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }
    }
}
