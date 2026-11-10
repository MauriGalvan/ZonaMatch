using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public class AnalysisFollowUpRepository(ZonaMatchDbContext context) : IAnalysisFollowUpRepository
{
    public Task<UserFeedbackPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.UserFeedbackPreferences.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public Task<AnalysisFollowUp?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.AnalysisFollowUps.Include(f => f.Analysis)
            .Where(f => f.UserId == userId && f.CompletedAt == null && f.PostponedUntil == null)
            .OrderBy(f => f.InvitedAt).FirstOrDefaultAsync(cancellationToken);

    public Task<AnalysisFollowUp?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        context.AnalysisFollowUps.SingleOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken);

    public Task<AnalysisFollowUp?> GetForAnalysisAsync(Guid analysisId, Guid userId, CancellationToken cancellationToken = default) =>
        context.AnalysisFollowUps.SingleOrDefaultAsync(f => f.AnalysisId == analysisId && f.UserId == userId, cancellationToken);

    public Task<Analysis?> GetEligibleAnalysisAsync(Guid userId, DateTimeOffset createdBefore, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.Analyses.Where(a => a.UserId == userId && a.OriginalAnalysisId == null && a.CreatedAt <= createdBefore)
            .Where(a => !context.AnalysisFollowUps.Any(f => f.AnalysisId == a.Id &&
                (f.CompletedAt != null || f.PostponedUntil > now)))
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).FirstOrDefaultAsync(cancellationToken);

    public void Add(AnalysisFollowUp followUp) => context.AnalysisFollowUps.Add(followUp);
    public void Add(UserFeedbackPreferences preferences) => context.UserFeedbackPreferences.Add(preferences);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("El seguimiento cambió en otra sesión. Actualizá la página.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("La invitación ya se creó en otra sesión. Actualizá la página.");
        }
    }
}
