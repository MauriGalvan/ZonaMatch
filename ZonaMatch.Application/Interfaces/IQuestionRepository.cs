using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IQuestionRepository
    {
        // Zone questions with their answers and authors, newest question first (read-only).
        Task<IReadOnlyList<Question>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default);

        Task<Question?> GetByIdAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Question question);
        void Add(Answer answer);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
