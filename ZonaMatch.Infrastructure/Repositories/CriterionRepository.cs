using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class CriterionRepository : ICriterionRepository
    {
        private readonly ZonaMatchDbContext _context;

        public CriterionRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Criterion>> ListAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Criteria
                .OrderBy(criterion => criterion.Order)
                .ToListAsync(cancellationToken);
        }
    }
}
