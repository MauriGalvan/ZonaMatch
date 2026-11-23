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

    public Task<IReadOnlyList<DomainQuestion>> ListLatestAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DomainQuestion>>(Questions.OrderByDescending(p => p.CreatedAt).Take(limit).ToList());

    public Task<DomainQuestion?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Questions.FirstOrDefault(p => p.Id == id));

    public Task<Answer?> GetAnswerAsync(Guid questionId, Guid answerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Questions.FirstOrDefault(p => p.Id == questionId)?.Answers.FirstOrDefault(answer => answer.Id == answerId));

    public List<DeletedQuestion> DeletedQuestions { get; } = [];

    public void Add(DeletedQuestion deletedQuestion) => DeletedQuestions.Add(deletedQuestion);

    public Task<IReadOnlyList<DeletedQuestion>> ListDeletedAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DeletedQuestion>>(DeletedQuestions.OrderByDescending(deleted => deleted.DeletedAt).Take(limit).ToList());

    public void Remove(DomainQuestion question) => Questions.Remove(question);

    public void Remove(Answer answer) => Questions.FirstOrDefault(p => p.Id == answer.QuestionId)?.Answers.Remove(answer);

    public Task<bool> ExistsAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Questions.Any(p => p.ZoneSlug == zoneSlug && p.Id == id));

    public Task<DomainQuestion?> GetByIdAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Questions.FirstOrDefault(p => p.ZoneSlug == zoneSlug && p.Id == id));

    public void Add(DomainQuestion question) => Questions.Add(question);

    public void Add(Answer answer) =>
        Questions.Single(question => question.Id == answer.QuestionId).Answers.Add(answer);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
