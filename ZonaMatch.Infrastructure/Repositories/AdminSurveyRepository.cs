using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public sealed class AdminSurveyRepository(ZonaMatchDbContext context) : IAdminSurveyRepository
{
    public async Task<AdminSurveyResponse> ListAsync(DateTimeOffset? from, DateTimeOffset? until,
        string? outcome, int? helpfulness, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.AnalysisFollowUps.AsNoTracking().Where(answer => answer.CompletedAt != null);
        if (from is { } start) query = query.Where(answer => answer.CompletedAt >= start);
        if (until is { } end) query = query.Where(answer => answer.CompletedAt < end);
        if (outcome is not null) query = query.Where(answer => answer.Outcome == outcome);
        if (helpfulness is not null) query = query.Where(answer => answer.Helpfulness == helpfulness);

        var groups = await query.GroupBy(answer => answer.Outcome)
            .Select(group => new { Outcome = group.Key!, Count = group.Count(),
                HelpfulnessSum = group.Sum(answer => answer.Helpfulness ?? 0) })
            .ToListAsync(cancellationToken);
        var total = groups.Sum(group => group.Count);
        var summary = new AdminSurveySummary(total,
            total == 0 ? null : (double)groups.Sum(group => group.HelpfulnessSum) / total,
            groups.OrderBy(group => group.Outcome)
                .Select(group => new AdminSurveyOutcomeCount(group.Outcome, group.Count)).ToList());
        var items = await query.OrderByDescending(answer => answer.CompletedAt).ThenByDescending(answer => answer.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(answer => new AdminSurveyAnswer(answer.Id, answer.AnalysisId, answer.CompletedAt!.Value,
                answer.Outcome!, answer.Helpfulness!.Value, answer.Comment))
            .ToListAsync(cancellationToken);
        return new AdminSurveyResponse(items, page, pageSize, summary);
    }
}
