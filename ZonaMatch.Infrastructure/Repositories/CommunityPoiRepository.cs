using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class CommunityPoiRepository : ICommunityPoiRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public CommunityPoiRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // FindAsync also sees a point added earlier in the same unit of work, before it is saved
        public async Task<CommunityPoi?> GetAsync(string id, CancellationToken cancellationToken = default) =>
            await _dbContext.CommunityPois.FindAsync([id], cancellationToken);

        public void Add(CommunityPoi poi) => _dbContext.CommunityPois.Add(poi);
    }
}
