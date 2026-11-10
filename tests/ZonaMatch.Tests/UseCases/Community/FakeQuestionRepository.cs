using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;

namespace ZonaMatch.Tests.UseCases.Community;

internal sealed class FakeQuestionRepository : IQuestionRepository
{
    public List<DomainQuestion> Questions { get; } = [];

    public Task<IReadOnlyList<DomainQuestion>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DomainQuestion>>(
            Questions.Where(p => p.ZoneSlug == zoneSlug).OrderByDescending(p => p.CreatedAt).ToList());

    public Task<bool> ExistsAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Questions.Any(p => p.ZoneSlug == zoneSlug && p.Id == id));

    public Task<DomainQuestion?> GetByIdAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Questions.FirstOrDefault(p => p.ZoneSlug == zoneSlug && p.Id == id));

    public void Add(DomainQuestion question) => Questions.Add(question);

    public void Add(Answer answer) =>
        Questions.Single(question => question.Id == answer.QuestionId).Answers.Add(answer);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
