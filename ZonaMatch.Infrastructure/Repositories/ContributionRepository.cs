using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ContributionRepository : IContributionRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public ContributionRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(int ApprovedCount, int PendingCount)> CountAsync(string zoneSlug, CancellationToken cancellationToken = default)
        {
            var counts = await _dbContext.Contributions
                .Where(contribution => contribution.ZoneSlug == zoneSlug && contribution.Status != ContributionStatus.Rejected)
                .GroupBy(contribution => contribution.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            int CountByStatus(ContributionStatus status) => counts.FirstOrDefault(count => count.Status == status)?.Count ?? 0;

            return (CountByStatus(ContributionStatus.Approved), CountByStatus(ContributionStatus.Pending));
        }

        public async Task<IReadOnlyList<Contribution>> ListPendingAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Contributions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(contribution => contribution.User)
                .Include(contribution => contribution.Votes)
                .Where(contribution => contribution.ZoneSlug == zoneSlug && contribution.Status == ContributionStatus.Pending)
                .OrderBy(contribution => contribution.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<Contribution?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Contributions
                .Include(contribution => contribution.User)
                .Include(contribution => contribution.Votes)
                .FirstOrDefaultAsync(contribution => contribution.ZoneSlug == zoneSlug && contribution.Id == id, cancellationToken);

        public void Add(Contribution contribution) => _dbContext.Contributions.Add(contribution);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // The same user submitted two votes at the same time.
                throw new ConflictException("Tu voto ya se habia registrado. Actualiza la pagina.");
            }
        }
    }
}
