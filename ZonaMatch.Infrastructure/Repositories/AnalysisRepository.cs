using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class AnalysisRepository : IAnalysisRepository
    {
        private readonly ZonaMatchDbContext _context;

        public AnalysisRepository(ZonaMatchDbContext context)
        {
            _context = context;
        }

        // Always load an analysis with its zones and complete criteria (members, places, and
        // preferences). Split queries avoid multiplying rows when loading several collections at once.
        private IQueryable<Analysis> WithDetails =>
            _context.Analyses
                .Include(a => a.Zones)
                .Include(a => a.Context).ThenInclude(c => c.People)
                .Include(a => a.Context).ThenInclude(c => c.Places)
                .Include(a => a.Context).ThenInclude(c => c.Criteria)
                .AsSplitQuery();

        public async Task<IReadOnlyList<Analysis>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            await WithDetails
                .Where(a => a.UserId == userId && a.DeletedAt == null)
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync(cancellationToken);

        public async Task<Analysis?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            await WithDetails.FirstOrDefaultAsync(
                a => a.Id == id && a.UserId == userId && a.DeletedAt == null,
                cancellationToken);

        public async Task<IReadOnlyList<Analysis>> ListVersionsAsync(Guid originalAnalysisId, CancellationToken cancellationToken = default) =>
            await WithDetails
                .Where(a => a.OriginalAnalysisId == originalAnalysisId && a.DeletedAt == null)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Analysis>> GetTrashedForUserAsync(
            Guid userId,
            DateTimeOffset deletedAfter,
            CancellationToken cancellationToken = default) =>
            await _context.Analyses
                .Where(a => a.UserId == userId && a.DeletedAt != null && a.DeletedAt > deletedAfter)
                .OrderByDescending(a => a.DeletedAt)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Analysis>> GetDeletedForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            await _context.Analyses
                .Include(a => a.Context)
                .Where(a => a.UserId == userId && a.DeletedAt != null)
                .OrderByDescending(a => a.DeletedAt)
                .ToListAsync(cancellationToken);

        public async Task<Analysis?> GetTrashedAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            await _context.Analyses
                .Include(a => a.Context)
                .FirstOrDefaultAsync(
                    a => a.Id == id && a.UserId == userId && a.DeletedAt != null,
                    cancellationToken);

        public async Task<Analysis?> GetIncludingDeletedAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            await _context.Analyses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        public async Task<IReadOnlyList<Analysis>> ListVersionsIncludingDeletedAsync(
            Guid originalAnalysisId,
            CancellationToken cancellationToken = default) =>
            await _context.Analyses
                .Include(a => a.Context)
                .Where(a => a.OriginalAnalysisId == originalAnalysisId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Analysis>> GetExpiredTrashedAsync(
            DateTimeOffset deletedBefore,
            CancellationToken cancellationToken = default) =>
            await _context.Analyses
                .Include(a => a.Context)
                .Where(a => a.DeletedAt != null && a.DeletedAt <= deletedBefore)
                .OrderBy(a => a.DeletedAt)
                .ToListAsync(cancellationToken);

        public void Add(Analysis analysis) => _context.Analyses.Add(analysis);

        public void PermanentlyDelete(Analysis analysis)
        {
            _context.Analyses.Remove(analysis);
            // The criteria snapshot belongs only to this analysis.
            _context.Contexts.Remove(analysis.Context);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
