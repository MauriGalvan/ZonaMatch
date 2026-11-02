using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IQuestionRepository
    {
        // Zone questions with their answers and authors, newest question first (read-only).
        Task<IReadOnlyList<Question>> ListByZoneAsync(string zonaSlug, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        Task<Question?> GetByIdAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Question pregunta);
        void Add(Answer respuesta);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
