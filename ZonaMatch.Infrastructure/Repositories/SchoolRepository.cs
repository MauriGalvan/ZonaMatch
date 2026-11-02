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

        public Task<IReadOnlyList<(School Entity, double DistanceMeters)>> GetNearbyByFiltersAsync(
            double latitude, double longitude, double radiusMeters, string? level, string? sector)
        {
            IQueryable<School> query = _dbContext.Set<School>();

            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(school => school.Level == level);

            if (!string.IsNullOrWhiteSpace(sector))
                query = query.Where(school => school.Sector == sector);

            return FindWithinRadiusAsync(query, latitude, longitude, radiusMeters);
        }
    }
}
