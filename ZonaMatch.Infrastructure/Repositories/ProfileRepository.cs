using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class ProfileRepository : IProfileRepository
    {
        private readonly ZonaMatchDbContext _context;

        public ProfileRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        public async Task<Context?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            await _context.Contexts
                .Include(c => c.People)
                .Include(c => c.Places)
                .Include(c => c.Criteria)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Type == ContextType.Profile, cancellationToken);

        public async Task<IReadOnlyList<Context>> GetByUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            if (userIds.Count == 0)
                return [];

            return await _context.Contexts
                .AsNoTracking()
                .Where(c => userIds.Contains(c.UserId) && c.Type == ContextType.Profile)
                .Include(c => c.People)
                .Include(c => c.Places)
                .Include(c => c.Criteria)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
        }

        public void Add(Context profile) => _context.Contexts.Add(profile);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two requests created the user's profile at the same time (unique index per user).
                throw new ConflictException("Tu perfil ya se había guardado. Actualizá la página.");
            }
        }
    }
}
