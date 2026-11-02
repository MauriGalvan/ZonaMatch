using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class DistrictRepository : PolygonRepository<District>, IDistrictRepository
    {
        public DistrictRepository(ZonaMatchDbContext dbContext) : base(dbContext)
        {
        }
    }
}
