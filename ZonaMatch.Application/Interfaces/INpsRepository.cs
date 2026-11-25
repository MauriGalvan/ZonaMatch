using ZonaMatch.Application.DTOs.Nps;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface INpsRepository
{
    Task RecordCompletionAsync(Guid userId, string fingerprint, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> IsExperiencedAsync(Guid userId, DateTimeOffset accountCreatedBefore, CancellationToken cancellationToken = default);
    Task<NpsSurvey?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NpsSurvey?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsInCooldownAsync(Guid userId, DateTimeOffset responseSince, DateTimeOffset postponementSince, CancellationToken cancellationToken = default);
    void Add(NpsSurvey survey);
    Task<AdminNpsResponse> ListResponsesAsync(DateTimeOffset? from, DateTimeOffset? until, int? score, int page, CancellationToken cancellationToken = default);
}
