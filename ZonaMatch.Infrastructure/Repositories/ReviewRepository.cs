using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public ReviewRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<Review>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Reviews
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.User)
                .Include(review => review.HelpfulVotes)
                .Where(review => review.ZoneSlug == zoneSlug)
                .OrderByDescending(review => review.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<Review?> GetByUserAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default) =>
            _dbContext.Reviews
                .Include(review => review.HelpfulVotes)
                .FirstOrDefaultAsync(review => review.ZoneSlug == zoneSlug && review.UserId == userId, cancellationToken);

        public Task<Review?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Reviews
                .Include(review => review.HelpfulVotes)
                .FirstOrDefaultAsync(review => review.ZoneSlug == zoneSlug && review.Id == id, cancellationToken);

        public void Add(Review review) => _dbContext.Reviews.Add(review);

        public void Delete(Review review) => _dbContext.Reviews.Remove(review);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two requests created the user's review for the zone at the same time or marked the same review useful.
                throw new ConflictException("La operación ya se había registrado. Actualizá la página.");
            }
        }
    }
}
