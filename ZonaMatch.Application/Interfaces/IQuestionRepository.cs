using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IQuestionRepository
    {
        // Zone questions with their answers and authors, newest question first (read-only).
        Task<IReadOnlyList<Question>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default);

        // The latest questions of every zone with their answers and authors, newest first (read-only).
        Task<IReadOnlyList<Question>> ListLatestAsync(int limit, CancellationToken cancellationToken = default);

        // A question of any zone with its author and answers, tracked so it can be removed.
        Task<Question?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default);

        Task<Question?> GetByIdAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Question question);
        void Add(Answer answer);

        // An answer of a question, tracked so it can be removed.
        Task<Answer?> GetAnswerAsync(Guid questionId, Guid answerId, CancellationToken cancellationToken = default);

        // Its answers are deleted with it (database cascade).
        void Remove(Question question);

        void Remove(Answer answer);

        // History of questions an administrator deleted.
        void Add(DeletedQuestion deletedQuestion);

        // The latest deleted questions, most recently deleted first (read-only).
        Task<IReadOnlyList<DeletedQuestion>> ListDeletedAsync(int limit, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
