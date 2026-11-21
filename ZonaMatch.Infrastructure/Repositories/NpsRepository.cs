using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.DTOs.Nps;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public sealed class NpsRepository(ZonaMatchDbContext context) : INpsRepository
{
    public async Task RecordCompletionAsync(Guid userId, string fingerprint, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.completed_zone_analyses (user_id, fingerprint, completed_at) VALUES ({userId}, {fingerprint}, {now}) ON CONFLICT (user_id, fingerprint) DO NOTHING", cancellationToken);

    public async Task<bool> IsExperiencedAsync(Guid userId, DateTimeOffset accountCreatedBefore, CancellationToken cancellationToken = default) =>
        await context.Users.AnyAsync(user => user.Id == userId && user.CreatedAt <= accountCreatedBefore, cancellationToken)
        && (await context.CompletedZoneAnalyses.Where(item => item.UserId == userId).Take(2).CountAsync(cancellationToken) >= 2
            // Existing users already have experience when they saved two completed original analyses.
            || await context.Analyses.Where(item => item.UserId == userId && item.OriginalAnalysisId == null && item.ResultJson != null).Take(2).CountAsync(cancellationToken) >= 2);

    public Task<NpsSurvey?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.NpsSurveys.SingleOrDefaultAsync(item => item.UserId == userId && item.CompletedAt == null && item.PostponedAt == null, cancellationToken);

    public Task<NpsSurvey?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        context.NpsSurveys.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);

    public Task<bool> IsInCooldownAsync(Guid userId, DateTimeOffset responseSince, DateTimeOffset postponementSince, CancellationToken cancellationToken = default) =>
        context.NpsSurveys.AnyAsync(item => item.UserId == userId && (item.CompletedAt > responseSince || item.PostponedAt > postponementSince), cancellationToken);

    public void Add(NpsSurvey survey) => context.NpsSurveys.Add(survey);

    public async Task<AdminNpsResponse> ListResponsesAsync(DateTimeOffset? from, DateTimeOffset? until, int? score, int page, CancellationToken cancellationToken = default)
    {
        const int pageSize = 20;
        var query = context.NpsSurveys.AsNoTracking().Where(item => item.CompletedAt != null);
        if (from is { } start) query = query.Where(item => item.CompletedAt >= start);
        if (until is { } end) query = query.Where(item => item.CompletedAt < end);
        if (score is not null) query = query.Where(item => item.Score == score);
        var groups = await query.GroupBy(item => item.Score).Select(group => new { Score = group.Key!.Value, Count = group.Count() }).ToListAsync(cancellationToken);
        var total = groups.Sum(group => group.Count);
        var promoters = groups.Where(group => group.Score >= 9).Sum(group => group.Count);
        var passives = groups.Where(group => group.Score is >= 7 and <= 8).Sum(group => group.Count);
        var detractors = groups.Where(group => group.Score <= 6).Sum(group => group.Count);
        var items = await query.OrderByDescending(item => item.CompletedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new NpsAnswerResponse(item.Id, item.CompletedAt!.Value, item.Score!.Value, item.Comment)).ToListAsync(cancellationToken);
        return new(items, page, pageSize, new(total, promoters, passives, detractors, total == 0 ? null : 100d * (promoters - detractors) / total));
    }
}
