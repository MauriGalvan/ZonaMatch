using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class EspacioVerdeRepository : GeoPuntoRepository<EspacioVerde>, IEspacioVerdeRepository
    {
        public EspacioVerdeRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }
    }
}
