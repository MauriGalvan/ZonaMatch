using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IQuestionRepository
    {
        // Preguntas de la zona con sus respuestas y autores, la pregunta mas nueva primero (solo lectura)
        Task<IReadOnlyList<Question>> ListByZoneAsync(string zonaSlug, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        Task<Question?> GetByIdAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Question pregunta);
        void Add(Answer respuesta);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
